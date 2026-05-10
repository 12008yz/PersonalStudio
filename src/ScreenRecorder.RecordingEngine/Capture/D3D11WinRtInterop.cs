using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenRecorder.RecordingEngine.Capture;

internal static class D3D11WinRtInterop
{
    [DllImport("d3d11.dll", ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    private static extern uint CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);

    internal static IDirect3DDevice CreateWinRtDeviceFromDxgiDevice(nint dxgiDeviceNative)
    {
        var hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDeviceNative, out var unk);
        if (hr != 0)
            throw new InvalidOperationException($"CreateDirect3D11DeviceFromDXGIDevice failed (HRESULT 0x{hr:X8}).");

        try
        {
            var device = Marshal.GetObjectForIUnknown(unk) as IDirect3DDevice;
            if (device is null)
                throw new InvalidOperationException("CreateDirect3D11DeviceFromDXGIDevice returned an unsupported COM object.");
            return device;
        }
        finally
        {
            if (unk != 0)
                Marshal.Release(unk);
        }
    }
}
