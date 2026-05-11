using NAudio.Wave;

namespace ScreenRecorder.RecordingEngine.Audio;

/// <summary>
/// Единый PCM-контракт для одновременного захвата нескольких источников:
/// помимо буфера и формата содержит тип источника (микрофон или loopback).
/// </summary>
public sealed class SourcedPcmCaptureDataAvailableEventArgs : EventArgs
{
    public SourcedPcmCaptureDataAvailableEventArgs(
        PcmCaptureSourceKind sourceKind,
        byte[] pcmSamples,
        WaveFormat waveFormat)
    {
        SourceKind = sourceKind;
        PcmSamples = pcmSamples ?? throw new ArgumentNullException(nameof(pcmSamples));
        WaveFormat = waveFormat ?? throw new ArgumentNullException(nameof(waveFormat));
    }

    public PcmCaptureSourceKind SourceKind { get; }

    public byte[] PcmSamples { get; }

    public WaveFormat WaveFormat { get; }
}
