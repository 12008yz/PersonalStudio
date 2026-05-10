using SharpGen.Runtime;
using Vortice.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>
/// Сопряжение <c>MFStartup</c>/<c>MFShutdown</c>: на процесс хватает парных вызовов; используем счётчик для общей библиотеки.
/// </summary>
public static class MediaFoundationLifetime
{
    private static readonly object Gate = new();
    private static int _refs;

    /// <summary>Увеличить счётчик и при первом вызове выполнить <see cref="MediaFactory.MFStartup"/>.</summary>
    public static void AddRef()
    {
        lock (Gate)
        {
            if (_refs == 0)
                MediaFactory.MFStartup().CheckError();

            _refs++;
        }
    }

    /// <summary>Уменьшить счётчик и при нуле вызвать <see cref="MediaFactory.MFShutdown"/>.</summary>
    public static void Release()
    {
        lock (Gate)
        {
            if (_refs == 0)
                return;

            _refs--;
            if (_refs == 0)
                MediaFactory.MFShutdown().CheckError();
        }
    }
}
