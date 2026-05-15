using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using MapFlags = Vortice.Direct3D11.MapFlags;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Копирует область BGRA8 с GPU-текстуры захвата в плотный CPU-буфер (staging + Map).</summary>
public sealed class Direct3D11BgraFrameReader : IDisposable
{
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly object _contextGate = new();
    private ID3D11Texture2D? _staging;
    private int _stagingWidth;
    private int _stagingHeight;

    public Direct3D11BgraFrameReader(ID3D11Device device)
    {
        ArgumentNullException.ThrowIfNull(device);
        _device = device;
        _context = device.ImmediateContext;
    }

    public void ReadBgraRegion(
        IDirect3DSurface surface,
        int width,
        int height,
        byte[] destination,
        out int bgraStride)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(destination);

        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Width and height must be positive.");

        if ((width & 1) != 0 || (height & 1) != 0)
            throw new ArgumentException("Width and height must be even.", nameof(width));

        bgraStride = width * 4;
        var requiredBytes = bgraStride * height;
        if (destination.Length < requiredBytes)
        {
            throw new ArgumentException(
                $"Destination must hold at least {requiredBytes} bytes for {width}x{height} BGRA.",
                nameof(destination));
        }

        using var sourceTexture = Direct3D11DxgiSurfaceInterop.OpenTexture2D(surface);
        var sourceDesc = sourceTexture.Description;
        if (sourceDesc.Width < (uint)width || sourceDesc.Height < (uint)height)
        {
            throw new ArgumentOutOfRangeException(
                nameof(surface),
                $"Surface is {sourceDesc.Width}x{sourceDesc.Height}, cannot read {width}x{height} region.");
        }

        lock (_contextGate)
        {
            EnsureStagingTexture(width, height);

            var box = new Box(0, 0, 0, width, height, 1);
            _context.CopySubresourceRegion(_staging!, 0, 0, 0, 0, sourceTexture, 0, box);

            var mapped = _context.Map(_staging!, 0, MapMode.Read, MapFlags.None);
            try
            {
                CopyTightBgra(mapped, width, height, destination);
            }
            finally
            {
                _context.Unmap(_staging!, 0);
            }
        }
    }

    public void Dispose()
    {
        lock (_contextGate)
        {
            _staging?.Dispose();
            _staging = null;
        }
    }

    private void EnsureStagingTexture(int width, int height)
    {
        if (_staging is not null && _stagingWidth == width && _stagingHeight == height)
            return;

        _staging?.Dispose();
        _staging = null;

        var desc = new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Staging,
            BindFlags = BindFlags.None,
            CPUAccessFlags = CpuAccessFlags.Read,
            MiscFlags = ResourceOptionFlags.None,
        };

        _staging = _device.CreateTexture2D(desc);
        _stagingWidth = width;
        _stagingHeight = height;
    }

    private static void CopyTightBgra(MappedSubresource mapped, int width, int height, byte[] destination)
    {
        var rowBytes = width * 4;
        var sourcePitch = mapped.RowPitch;

        for (var row = 0; row < height; row++)
        {
            var srcRow = mapped.DataPointer + row * (nint)sourcePitch;
            var dstRow = row * rowBytes;
            unsafe
            {
                new ReadOnlySpan<byte>((void*)srcRow, rowBytes).CopyTo(destination.AsSpan(dstRow, rowBytes));
            }
        }
    }
}
