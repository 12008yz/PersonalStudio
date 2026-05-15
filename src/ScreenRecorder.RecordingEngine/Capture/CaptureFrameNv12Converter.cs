using ScreenRecorder.RecordingEngine.MediaFoundation;
using Windows.Graphics;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>
/// Захватный кадр (WinRT <c>IDirect3DSurface</c>) → NV12 в CPU-памяти для <see cref="Mp4SinkWriter"/>.
/// Поверхность должна принадлежать тому же <see cref="WinRtGraphicsDevice"/>, что передан в конструктор.
/// Вызовы сериализуются внутри (D3D11 immediate context не потокобезопасен).
/// Оптимизация через шейдер — отдельный этап.
/// </summary>
public sealed class CaptureFrameNv12Converter : IDisposable
{
    private readonly Direct3D11BgraFrameReader _bgraReader;
    private byte[] _bgraScratch = [];
    private byte[] _nv12Scratch = [];

    public CaptureFrameNv12Converter(WinRtGraphicsDevice graphicsDevice)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        _bgraReader = new Direct3D11BgraFrameReader(graphicsDevice.D3D11Device);
    }

    /// <summary>
    /// Конвертирует кадр в NV12. Нечётные <paramref name="contentSize"/> усекаются на 1 px.
    /// </summary>
    public Nv12ConvertedFrame Convert(IDirect3DSurface surface, SizeInt32 contentSize)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var evenSize = CaptureFrameDimensions.TruncateToEven(contentSize);
        CaptureFrameDimensions.ValidateEvenMinimum(evenSize);

        var width = evenSize.Width;
        var height = evenSize.Height;

        var bgraBytes = width * height * 4;
        if (_bgraScratch.Length < bgraBytes)
            _bgraScratch = new byte[bgraBytes];

        _bgraReader.ReadBgraRegion(surface, width, height, _bgraScratch, out var bgraStride);

        var nv12Bytes = Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(width, height);
        if (_nv12Scratch.Length != nv12Bytes)
            _nv12Scratch = new byte[nv12Bytes];

        BgraToNv12Converter.Convert(_bgraScratch, width, height, bgraStride, _nv12Scratch);
        return new Nv12ConvertedFrame(_nv12Scratch.AsMemory(0, nv12Bytes), width, height);
    }

    /// <summary>
    /// Пишет NV12 в <paramref name="destination"/> (без переиспользования внутреннего буфера результата).
    /// </summary>
    public SizeInt32 ConvertTo(IDirect3DSurface surface, SizeInt32 contentSize, Span<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var evenSize = CaptureFrameDimensions.TruncateToEven(contentSize);
        CaptureFrameDimensions.ValidateEvenMinimum(evenSize);

        var width = evenSize.Width;
        var height = evenSize.Height;
        var nv12Bytes = Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(width, height);
        if (destination.Length < nv12Bytes)
        {
            throw new ArgumentException(
                $"Destination must hold at least {nv12Bytes} bytes for {width}x{height} NV12.",
                nameof(destination));
        }

        var bgraBytes = width * height * 4;
        if (_bgraScratch.Length < bgraBytes)
            _bgraScratch = new byte[bgraBytes];

        _bgraReader.ReadBgraRegion(surface, width, height, _bgraScratch, out var bgraStride);
        BgraToNv12Converter.Convert(_bgraScratch, width, height, bgraStride, destination[..nv12Bytes]);
        return evenSize;
    }

    public void Dispose() => _bgraReader.Dispose();
}
