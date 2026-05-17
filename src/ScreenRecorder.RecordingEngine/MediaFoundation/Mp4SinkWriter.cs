using System.Runtime.InteropServices;
using Vortice.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>
/// Запись MP4 (H.264 + AAC-LC) через <see cref="IMFSinkWriter"/>.
/// Все вызовы MF выполняются на потоке создателя; для продакшена — worker <see cref="BoundedEncoderWorkQueue{TWorkItem}"/>.
/// </summary>
public sealed class Mp4SinkWriter : IDisposable
{
    private readonly Mp4SinkWriterConfiguration _configuration;
    private readonly IMFSinkWriter _sinkWriter;
    private readonly int _videoStreamIndex;
    private readonly int _audioStreamIndex;
    private readonly long _videoFrameDurationHns;
    private bool _mfLifetimeHeld;
    private bool _finalized;

    private Mp4SinkWriter(
        Mp4SinkWriterConfiguration configuration,
        IMFSinkWriter sinkWriter,
        int videoStreamIndex,
        int audioStreamIndex,
        long videoFrameDurationHns)
    {
        _configuration = configuration;
        _sinkWriter = sinkWriter;
        _videoStreamIndex = videoStreamIndex;
        _audioStreamIndex = audioStreamIndex;
        _videoFrameDurationHns = videoFrameDurationHns;
    }

    public long VideoFrameDurationHns => _videoFrameDurationHns;

    public static Mp4SinkWriter Create(string outputPath, Mp4SinkWriterConfiguration configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        var availability = MediaFoundationEncoderAvailability.Probe();
        if (!availability.IsSufficientForRecording)
        {
            throw new InvalidOperationException(
                $"No suitable encoders: H.264={availability.H264VideoEncoderCount}, AAC={availability.AacAudioEncoderCount}.");
        }

        MediaFoundationLifetime.AddRef();
        IMFSinkWriter? sinkWriter = null;
        try
        {
            using var attributes = MediaFactory.MFCreateAttributes(2);
            if (configuration.EnableHardwareTransforms)
            {
                attributes.Set(SinkWriterAttributeKeys.ReadwriteEnableHardwareTransforms, 1u);
            }

            attributes.Set(TranscodeAttributeKeys.TranscodeContainertype, TranscodeContainerTypeGuids.Mpeg4);

            sinkWriter = MediaFactory.MFCreateSinkWriterFromURL(outputPath, null, attributes);

            using var videoOutputType = Mp4SinkWriterMediaTypes.CreateH264OutputType(configuration);
            var videoStreamIndex = sinkWriter.AddStream(videoOutputType);

            using var videoInputType = Mp4SinkWriterMediaTypes.CreateNv12InputType(configuration);
            using var videoEncoderParameters = Mp4H264EncoderParameters.CreateEncodingAttributes(configuration);
            sinkWriter.SetInputMediaType(videoStreamIndex, videoInputType, videoEncoderParameters);

            using var audioOutputType = Mp4SinkWriterMediaTypes.CreateAacOutputType(configuration);
            var audioStreamIndex = sinkWriter.AddStream(audioOutputType);

            using var audioInputType = Mp4SinkWriterMediaTypes.CreatePcmInputType(configuration);
            sinkWriter.SetInputMediaType(audioStreamIndex, audioInputType, null);

            sinkWriter.BeginWriting();

            var frameDuration = Mp4SinkWriterMediaTypes.FrameDurationHns(configuration.FramesPerSecond);
            var writer = new Mp4SinkWriter(configuration, sinkWriter, videoStreamIndex, audioStreamIndex, frameDuration)
            {
                _mfLifetimeHeld = true,
            };
            sinkWriter = null;
            return writer;
        }
        catch
        {
            sinkWriter?.Dispose();
            MediaFoundationLifetime.Release();
            throw;
        }
    }

    public void WriteVideoFrame(ReadOnlySpan<byte> nv12, long timestampHns)
    {
        ThrowIfFinalized();
        var expectedBytes = Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(_configuration.Width, _configuration.Height);
        if (nv12.Length != expectedBytes)
        {
            throw new ArgumentException(
                $"NV12 buffer length {nv12.Length} does not match {_configuration.Width}x{_configuration.Height} ({expectedBytes} bytes).",
                nameof(nv12));
        }

        WriteBufferSample(_videoStreamIndex, nv12, timestampHns, _videoFrameDurationHns);
    }

    public void WriteAudioPcm16(ReadOnlySpan<byte> pcm16Le, long timestampHns, long durationHns)
    {
        ThrowIfFinalized();
        if (pcm16Le.Length == 0)
            return;

        var bytesPerSampleFrame = _configuration.AudioChannels * 2;
        if (pcm16Le.Length % bytesPerSampleFrame != 0)
        {
            throw new ArgumentException(
                $"PCM16 length {pcm16Le.Length} is not aligned to {_configuration.AudioChannels} channel frames.",
                nameof(pcm16Le));
        }

        WriteBufferSample(_audioStreamIndex, pcm16Le, timestampHns, durationHns);
    }

    public void FinalizeWriting()
    {
        if (_finalized)
            return;

        _sinkWriter.Finalize();
        _finalized = true;
    }

    public void Dispose()
    {
        if (!_finalized)
        {
            try
            {
                FinalizeWriting();
            }
            catch
            {
                // Best-effort finalize on dispose; caller should prefer explicit FinalizeWriting after errors.
            }
        }

        _sinkWriter.Dispose();

        if (_mfLifetimeHeld)
        {
            MediaFoundationLifetime.Release();
            _mfLifetimeHeld = false;
        }
    }

    private void WriteBufferSample(int streamIndex, ReadOnlySpan<byte> data, long timestampHns, long durationHns)
    {
        if (durationHns <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationHns), "Sample duration must be positive.");

        using var buffer = MediaFactory.MFCreateMemoryBuffer(data.Length);
        buffer.Lock(out var pointer, out var maxLength, out _);
        if (data.Length > maxLength)
            throw new InvalidOperationException("MF memory buffer is smaller than sample payload.");

        try
        {
            Marshal.Copy(data.ToArray(), 0, pointer, data.Length);
        }
        finally
        {
            buffer.Unlock();
        }

        buffer.CurrentLength = data.Length;

        using var sample = MediaFactory.MFCreateSample();
        sample.AddBuffer(buffer);
        sample.SampleTime = timestampHns;
        sample.SampleDuration = durationHns;
        _sinkWriter.WriteSample(streamIndex, sample);
    }

    private void ThrowIfFinalized()
    {
        if (_finalized)
            throw new InvalidOperationException("Sink writer is finalized.");
    }
}
