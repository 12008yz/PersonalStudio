using System.Runtime.InteropServices;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>
/// Сопоставление исключений WinRT/COM/D3D с <see cref="ScreenCaptureFailureKind"/> для UI и логирования.
/// </summary>
public static class ScreenCaptureFailureClassifier
{
    // FACILITY_WIN32 HRESULT_FROM_WIN32(ERROR_ACCESS_DENIED) etc.
    private const uint E_AccessDenied = 0x80070005;
    private const uint E_InvalidArg = 0x80070057;
    private const uint E_Busy = 0x800700AA;

    private const uint DxgI_Error_AccessLost = 0x887A0026;
    private const uint DxgI_Error_DeviceRemoved = 0x887A0005;
    private const uint DxgI_Error_InvalidCall = 0x887A0001;

    /// <summary>Классифицирует исключение по цепочке <see cref="Exception.InnerException"/>.</summary>
    /// <remarks>
    /// Для <see cref="AggregateException"/> просматриваются все внутренние после <see cref="AggregateException.Flatten"/>; возвращается первая известная категория, иначе <see cref="ScreenCaptureFailureKind.Unknown"/>.
    /// </remarks>
    public static ScreenCaptureFailureKind Classify(Exception? ex)
    {
        if (ex is null)
            return ScreenCaptureFailureKind.Unknown;

        if (ex is AggregateException aggregate)
        {
            foreach (var inner in aggregate.Flatten().InnerExceptions)
            {
                var k = Classify(inner);
                if (k != ScreenCaptureFailureKind.Unknown)
                    return k;
            }

            return ScreenCaptureFailureKind.Unknown;
        }

        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is UnauthorizedAccessException)
                return ScreenCaptureFailureKind.AccessDenied;

            if (cur is ObjectDisposedException)
                return ScreenCaptureFailureKind.ObjectDisposedOrClosed;

            if (cur is COMException com)
            {
                var code = unchecked((uint)unchecked((int)com.HResult));
                switch (code)
                {
                    case E_AccessDenied:
                        return ScreenCaptureFailureKind.AccessDenied;
                    case E_Busy:
                        return ScreenCaptureFailureKind.ResourceBusy;
                    case E_InvalidArg:
                    case DxgI_Error_InvalidCall:
                        return ScreenCaptureFailureKind.InvalidArgument;
                    case DxgI_Error_AccessLost:
                    case DxgI_Error_DeviceRemoved:
                        return ScreenCaptureFailureKind.AccessLostOrDeviceFailed;
                }
            }

            if (cur is ArgumentException or ArgumentOutOfRangeException)
                return ScreenCaptureFailureKind.InvalidArgument;
        }

        return ScreenCaptureFailureKind.Unknown;
    }
}
