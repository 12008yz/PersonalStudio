using System.Runtime.InteropServices;
using Vortice.Direct3D11;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Доступ к нативным DXGI/D3D11 интерфейсам WinRT <see cref="IDirect3DSurface"/>.</summary>
internal static class Direct3D11DxgiSurfaceInterop
{
    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        void GetInterface(in Guid iid, out nint p);
    }

    public static ID3D11Texture2D OpenTexture2D(IDirect3DSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        var access = (IDirect3DDxgiInterfaceAccess)surface;
        var iid = typeof(ID3D11Texture2D).GUID;
        access.GetInterface(iid, out var native);
        if (native == 0)
            throw new InvalidOperationException("IDirect3DSurface did not provide ID3D11Texture2D.");

        return new ID3D11Texture2D(native);
    }
}
