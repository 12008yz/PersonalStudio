namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Итог <see cref="Mp4SinkWriter.Shutdown"/> — для логов и решений UI (файл сохранён / удалён).</summary>
public sealed class Mp4SinkWriterShutdownResult
{
    public Mp4SinkWriterShutdownResult(
        Mp4SinkWriterShutdownKind kind,
        bool finalizeSucceeded,
        bool hasWrittenSamples,
        bool outputFileRetained,
        bool outputFileDeleted,
        Exception? finalizeError)
    {
        Kind = kind;
        FinalizeSucceeded = finalizeSucceeded;
        HasWrittenSamples = hasWrittenSamples;
        OutputFileRetained = outputFileRetained;
        OutputFileDeleted = outputFileDeleted;
        FinalizeError = finalizeError;
    }

    public Mp4SinkWriterShutdownKind Kind { get; }

    public bool FinalizeSucceeded { get; }

    public bool HasWrittenSamples { get; }

    /// <summary>После shutdown файл по <see cref="Mp4SinkWriter.OutputPath"/> остался на диске.</summary>
    public bool OutputFileRetained { get; }

    public bool OutputFileDeleted { get; }

    public Exception? FinalizeError { get; }
}
