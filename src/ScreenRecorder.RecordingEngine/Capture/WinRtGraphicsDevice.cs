using System.Runtime.InteropServices;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Owns a D3D11 device and the WinRT <see cref="IDirect3DDevice"/> wrapper required by Windows.Graphics.Capture.</summary>
public sealed class WinRtGraphicsDevice : IDisposable
{
    private static readonly FeatureLevel[] FeatureLevels =
    [
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
    ];

    private readonly ID3D11Device _d3d11;

    private WinRtGraphicsDevice(ID3D11Device d3d11, IDirect3DDevice winRt)
    {
        _d3d11 = d3d11;
        WinRt = winRt;
    }

    public IDirect3DDevice WinRt { get; }

    public static WinRtGraphicsDevice CreateHardwareOrWarp()
    {
        var flags = DeviceCreationFlags.BgraSupport | DeviceCreationFlags.VideoSupport;
        var hr = D3D11.D3D11CreateDevice(
            null,
            DriverType.Hardware,
            flags,
            FeatureLevels,
            out var device,
            out _,
            out var context);

        if (hr.Failure)
        {
            hr = D3D11.D3D11CreateDevice(
                null,
                DriverType.Warp,
                flags,
                FeatureLevels,
                out device,
                out _,
                out context);
            hr.CheckError();
        }

        ID3D11Device? ownedDevice = device;
        try
        {
            using (context)
            {
                using var dxgiDevice = ownedDevice!.QueryInterface<IDXGIDevice>();
                var winRt = D3D11WinRtInterop.CreateWinRtDeviceFromDxgiDevice(dxgiDevice.NativePointer);
                var result = new WinRtGraphicsDevice(ownedDevice, winRt);
                ownedDevice = null;
                return result;
            }
        }
        finally
        {
            ownedDevice?.Dispose();
        }
    }

    public void Dispose()
    {
        // WinRT IDirect3DDevice из CreateDirect3D11DeviceFromDXGIDevice: RCW не поддерживает cast к System.IDisposable
        // (E_NOINTERFACE / IID 805D7A98-...); закрытие через WinRT IClosable.Close() — вызываем динамически (CsWinRT vs Marshal.GetObjectForIUnknown).
        try
        {
            dynamic w = WinRt;
            w.Close();
        }
        catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            // нет метода Close — полагаемся на GC для WinRT-обёртки
        }
        catch (Exception)
        {
            // иные ошибки Close — всё равно освобождаем D3D11 ниже
        }
        finally
        {
            _d3d11.Dispose();
        }
    }
}
