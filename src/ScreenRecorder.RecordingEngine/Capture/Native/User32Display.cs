using System.Runtime.InteropServices;

namespace ScreenRecorder.RecordingEngine.Capture.Native;

[StructLayout(LayoutKind.Sequential)]
internal readonly struct RectNative
{
    internal readonly int Left;
    internal readonly int Top;
    internal readonly int Right;
    internal readonly int Bottom;
}

/// <summary>MONITORINFOEX for <see cref="User32Display.GetMonitorInfo"/>.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
internal struct MonitorInfoExNative
{
    internal int cbSize;
    internal RectNative rcMonitor;
    internal RectNative rcWork;
    internal uint dwFlags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    internal string szDevice;
}

internal static class User32Display
{
    internal const uint MonitorinfofPrimary = 1;

    internal delegate bool MonitorEnumProc(
        nint hMonitor,
        nint hdcMonitor,
        ref RectNative lprcMonitor,
        nint dwData);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        nint hdc,
        nint lprcClip,
        MonitorEnumProc lpfnEnum,
        nint dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint hMonitor, ref MonitorInfoExNative lpmi);
}
