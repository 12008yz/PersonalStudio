namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Один зарегистрированный MFT-энкодер (для матрицы железа и диагностики).</summary>
public sealed record MediaFoundationEncoderInfo(
    Guid TransformClsid,
    string FriendlyName,
    bool IsHardware,
    MediaFoundationEncoderKind Kind);

public enum MediaFoundationEncoderKind
{
    H264Video = 0,
    AacAudio = 1,
}
