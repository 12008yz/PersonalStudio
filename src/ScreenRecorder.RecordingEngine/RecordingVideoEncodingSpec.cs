namespace ScreenRecorder.RecordingEngine;

/// <summary>
/// Зафиксированные параметры H.264 для MVP (фаза D): GOP и режим битрейта.
/// </summary>
public static class RecordingVideoEncodingSpec
{
    /// <summary>Интервал IDR/keyframe по умолчанию (секунды). 2 с — баланс seek и размера GOP для 30 fps.</summary>
    public const int DefaultKeyframeIntervalSeconds = 2;

    /// <summary>
    /// MVP: peak-constrained VBR — средний битрейт ближе к целевому, пики ограничены (статичный рабочий стол экономит биты).
    /// CBR доступен через <see cref="H264RateControlMode.ConstantBitrate"/> в конфигурации mux.
    /// </summary>
    public const H264RateControlMode DefaultRateControlMode = H264RateControlMode.PeakConstrainedVbr;

    /// <summary>Максимальный битрейт = средний × числитель / знаменатель (по умолчанию 3/2).</summary>
    public const int DefaultPeakBitrateNumerator = 3;

    public const int DefaultPeakBitrateDenominator = 2;
}

/// <summary>Режим контроля битрейта H.264 MFT (CODECAPI / MF_MT_H264_RATE_CONTROL_MODES).</summary>
public enum H264RateControlMode
{
    /// <summary>Постоянный битрейт (предсказуемый размер файла).</summary>
    ConstantBitrate = 0,

    /// <summary>VBR с потолком — рекомендуемый MVP для захвата экрана.</summary>
    PeakConstrainedVbr = 1,

    /// <summary>Неконтролируемый VBR (пики не ограничены явно).</summary>
    UnconstrainedVbr = 2,

    Quality = 3,
}
