using System.Runtime.InteropServices;
using Vortice.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>
/// Запись MP4 (H.264 + AAC-LC) через <see cref="IMFSinkWriter"/>.
/// Все вызовы MF выполняются на потоке создателя; для продакшена — worker <see cref="BoundedEncoderWorkQueue{TWorkItem}"/>.
/// Нормальный Stop — <see cref="Shutdown(Mp4SinkWriterShutdownKind.Complete)"/>; сбой — <see cref="Shutdown(Mp4SinkWriterShutdownKind.AbortDueToError)"/>.
/// После <see cref="Shutdown"/> объект освобождён; повторный вызов идемпотентен. <see cref="Dispose"/> без Shutdown — abort (best-effort).
/// </summary>
public sealed class Mp4SinkWriter : IDisposable
{
    private readonly string _outputPath;
    private readonly Mp4SinkWriterConfiguration _configuration;
    private readonly IMFSinkWriter _sinkWriter;
    private readonly int _videoStreamIndex;
    private readonly int _audioStreamIndex;
    private readonly long _videoFrameDurationHns;
    private bool _mfLifetimeHeld;
    private bool _finalized;
    private bool _disposed;
    private bool _hasWrittenSamples;

    private Mp4SinkWriter(
        string outputPath,
        Mp4SinkWriterConfiguration configuration,
        IMFSinkWriter sinkWriter,
        int videoStreamIndex,
        int audioStreamIndex,
        long videoFrameDurationHns)
    {
        _outputPath = outputPath;
        _configuration = configuration;
        _sinkWriter = sinkWriter;
        _videoStreamIndex = videoStreamIndex;
        _audioStreamIndex = audioStreamIndex;
        _videoFrameDurationHns = videoFrameDurationHns;
    }

    public string OutputPath => _outputPath;

    public long VideoFrameDurationHns => _videoFrameDurationHns;

    public bool IsFinalized => _finalized;

    public bool HasWrittenSamples => _hasWrittenSamples;

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
            var writer = new Mp4SinkWriter(outputPath, configuration, sinkWriter, videoStreamIndex, audioStreamIndex, frameDuration)
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
            TryDeleteOutputFile(outputPath);
            throw;
        }
    }

    public void WriteVideoFrame(ReadOnlySpan<byte> nv12, long timestampHns)
    {
        ThrowIfNotWritable();
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
        ThrowIfNotWritable();
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

    /// <summary>Явный успешный Finalize без освобождения writer (редко нужен; предпочтительнее <see cref="Shutdown"/>).</summary>
    public void FinalizeWriting()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Mp4SinkWriter));

        if (_finalized)
            return;

        _sinkWriter.Finalize();
        _finalized = true;
    }

    /// <summary>
    /// Контролируемое завершение mux. При <see cref="Mp4SinkWriterShutdownKind.Complete"/> и сбое finalize
    /// после записи семплов бросает <see cref="InvalidOperationException"/>.
    /// </summary>
    public Mp4SinkWriterShutdownResult Shutdown(Mp4SinkWriterShutdownKind kind)
    {
        if (_disposed)
        {
            return new Mp4SinkWriterShutdownResult(
                kind,
                finalizeSucceeded: _finalized,
                hasWrittenSamples: _hasWrittenSamples,
                outputFileRetained: File.Exists(_outputPath),
                outputFileDeleted: false,
                finalizeError: null);
        }

        var finalizeSucceeded = TryFinalize(out var finalizeError);
        ReleaseResources();

        var shouldDelete = ShouldDeleteOutputFile(finalizeSucceeded);
        if (shouldDelete)
            TryDeleteOutputFile(_outputPath);

        var outputFileRetained = File.Exists(_outputPath);
        var outputFileDeleted = shouldDelete && !outputFileRetained;

        var result = new Mp4SinkWriterShutdownResult(
            kind,
            finalizeSucceeded,
            _hasWrittenSamples,
            outputFileRetained,
            outputFileDeleted,
            finalizeError);

        if (kind == Mp4SinkWriterShutdownKind.Complete &&
            _hasWrittenSamples &&
            !finalizeSucceeded)
        {
            throw new InvalidOperationException(
                $"Failed to finalize MP4 at '{_outputPath}'.",
                finalizeError);
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            Shutdown(Mp4SinkWriterShutdownKind.AbortDueToError);
        }
        catch
        {
            // Best-effort abort on dispose; explicit Shutdown(Complete) is required for normal Stop.
        }
    }

    private bool ShouldDeleteOutputFile(bool finalizeSucceeded)
    {
        if (!_hasWrittenSamples)
            return true;

        return !finalizeSucceeded;
    }

    private bool TryFinalize(out Exception? error)
    {
        error = null;
        if (_finalized)
            return true;

        try
        {
            _sinkWriter.Finalize();
            _finalized = true;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    private void ReleaseResources()
    {
        if (_disposed)
            return;

        _disposed = true;
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
        _hasWrittenSamples = true;
    }

    private void ThrowIfNotWritable()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Mp4SinkWriter));

        if (_finalized)
            throw new InvalidOperationException("Sink writer is finalized.");
    }

    private static bool TryDeleteOutputFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                return true;
            }
        }
        catch
        {
            // Best-effort; caller logs via shutdown result + file still on disk.
        }

        return false;
    }
}
