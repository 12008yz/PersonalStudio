namespace ScreenRecorder.RecordingEngine.Recording;

/// <summary>Пайплайн, которому перед стартом сессии передаётся общая ось времени.</summary>
internal interface IRecordingSessionTimebaseConsumer
{
    void BindSessionTimebase(RecordingSessionTimebase timebase);
}
