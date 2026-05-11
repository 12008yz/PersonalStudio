using NAudio.CoreAudioApi;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>Варианты флагов <see cref="AudioClientStreamFlags"/> для loopback (совместимо с версиями NAudio).</summary>
internal enum LoopbackInitFlagStyle
{
    /// <summary><see cref="AudioClientStreamFlags.Loopback"/> + <see cref="AudioClientStreamFlags.AutoConvertPcm"/> без SRC quality.</summary>
    NoSrcDefaultQuality,

    /// <summary>Только <see cref="AudioClientStreamFlags.Loopback"/>.</summary>
    MinimalLoopbackOnly,

    /// <summary>Как <see cref="NAudio.Wave.WasapiLoopbackCapture"/> — с <see cref="AudioClientStreamFlags.SrcDefaultQuality"/>.</summary>
    NAudioDefaultWithSrcQuality,
}

internal static class LoopbackInitFlagStyleExtensions
{
    /// <returns>Флаги потока с <see cref="AudioClientStreamFlags.Loopback"/>.</returns>
    public static AudioClientStreamFlags ToLoopbackFlags(this LoopbackInitFlagStyle style) =>
        style switch
        {
            LoopbackInitFlagStyle.NoSrcDefaultQuality =>
                AudioClientStreamFlags.Loopback | AudioClientStreamFlags.AutoConvertPcm,
            LoopbackInitFlagStyle.MinimalLoopbackOnly => AudioClientStreamFlags.Loopback,
            LoopbackInitFlagStyle.NAudioDefaultWithSrcQuality =>
                AudioClientStreamFlags.Loopback
                | AudioClientStreamFlags.AutoConvertPcm
                | AudioClientStreamFlags.SrcDefaultQuality,
            _ => throw new ArgumentOutOfRangeException(nameof(style)),
        };
}

/// <summary>
/// WASAPI loopback через <see cref="WasapiCapture"/> с задаваемой длиной буфера.
/// Стандартный <see cref="NAudio.Wave.WasapiLoopbackCapture"/> всегда использует 100 ms, из‑за чего на части драйверов <c>IAudioClient.Initialize</c> даёт E_INVALIDARG.
/// </summary>
internal sealed class ConfigurableLoopbackWasapiCapture : WasapiCapture
{
    private readonly LoopbackInitFlagStyle _flagStyle;
    private readonly AudioClientStreamFlags _extraStreamFlags;

    public ConfigurableLoopbackWasapiCapture(
        MMDevice renderDevice,
        int audioBufferMillisecondsLength,
        LoopbackInitFlagStyle flagStyle,
        AudioClientStreamFlags extraStreamFlags = default)
        : base(renderDevice, false, audioBufferMillisecondsLength)
    {
        _flagStyle = flagStyle;
        _extraStreamFlags = extraStreamFlags;
    }

    protected override AudioClientStreamFlags GetAudioClientStreamFlags() =>
        _flagStyle.ToLoopbackFlags() | _extraStreamFlags;
}
