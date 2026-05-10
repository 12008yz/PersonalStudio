using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Creates <see cref="GraphicsCaptureItem"/> for Win32 monitor handles (C#/WinRT: <c>T.As&lt;TInterop&gt;()</c> + <c>FromAbi</c>).</summary>
public static class GraphicsCaptureItemFactory
{
    private static readonly Guid GraphicsCaptureItemGuid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(nint window, ref Guid iid);

        nint CreateForMonitor(nint monitor, ref Guid iid);
    }

    public static GraphicsCaptureItem CreateForMonitor(nint hmonitor)
    {
        var interop = GraphicsCaptureItem.As<IGraphicsCaptureItemInterop>();
        var iid = GraphicsCaptureItemGuid;
        var itemPointer = interop.CreateForMonitor(hmonitor, ref iid);
        if (itemPointer == 0)
            throw new InvalidOperationException("CreateForMonitor returned null (access denied, invalid HMONITOR, or capture unavailable).");
        try
        {
            return GraphicsCaptureItem.FromAbi(itemPointer);
        }
        finally
        {
            if (itemPointer != 0)
                Marshal.Release(itemPointer);
        }
    }
}
