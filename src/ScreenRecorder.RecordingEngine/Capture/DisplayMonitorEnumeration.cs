using System.ComponentModel;
using System.Runtime.InteropServices;
using ScreenRecorder.RecordingEngine.Capture.Native;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Enumerate monitors and pair each with an <c>HMONITOR</c> for Windows.Graphics.Capture interop.</summary>
public static class DisplayMonitorEnumeration
{
    /// <summary>Returns one entry per monitor, in enumeration order.</summary>
    public static IReadOnlyList<DisplayMonitorDescriptor> EnumerateMonitors()
    {
        var list = new List<DisplayMonitorDescriptor>();

        bool Callback(nint hMonitor, nint _, ref RectNative __, nint ___)
        {
            var mi = new MonitorInfoExNative
            {
                cbSize = Marshal.SizeOf<MonitorInfoExNative>(),
            };

            if (!User32Display.GetMonitorInfo(hMonitor, ref mi))
                return true;

            var b = mi.rcMonitor;
            list.Add(new DisplayMonitorDescriptor(
                hMonitor,
                mi.szDevice,
                (mi.dwFlags & User32Display.MonitorinfofPrimary) != 0,
                new DisplayMonitorBounds(b.Left, b.Top, b.Right, b.Bottom)));

            return true;
        }

        User32Display.MonitorEnumProc proc = Callback;
        if (!User32Display.EnumDisplayMonitors(0, 0, proc, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "EnumDisplayMonitors failed.");

        return list;
    }
}
