namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Как завершать <see cref="Mp4SinkWriter"/> — нормальный Stop или сбой пайплайна.</summary>
public enum Mp4SinkWriterShutdownKind
{
    /// <summary>Контролируемый Stop: требуется успешный <c>IMFSinkWriter::Finalize</c> при наличии семплов.</summary>
    Complete = 0,

    /// <summary>
    /// Сбой пайплайна: best-effort finalize. Файл удаляется, если семплов не было или finalize не удался;
    /// при успешном finalize частичный клип сохраняется (без исключения).
    /// </summary>
    AbortDueToError = 1,
}
