namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>
/// Укрупнённая классификация сбоев Windows.Graphics.Capture / D3D для пользовательских сообщений и логов.
/// </summary>
public enum ScreenCaptureFailureKind
{
    /// <summary>Не удалось отнести к известной категии.</summary>
    Unknown,

    /// <summary>Нет прав / политика конфиденциальности Windows (часто E_ACCESSDENIED).</summary>
    AccessDenied,

    /// <summary>Ресурс занят или недоступен (ERROR_BUSY и аналоги).</summary>
    ResourceBusy,

    /// <summary>Потеря доступа к устройству вывода, сбой GPU, смена режима (DXGI access lost / device removed и т.п.).</summary>
    AccessLostOrDeviceFailed,

    /// <summary>Некорректные аргументы или состояние (E_INVALIDARG, спорный монитор).</summary>
    InvalidArgument,

    /// <summary>Объект уже освобождён или сессия закрыта.</summary>
    ObjectDisposedOrClosed,
}
