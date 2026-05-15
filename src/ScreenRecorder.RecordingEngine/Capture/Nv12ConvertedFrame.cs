namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>
/// NV12-кадр после конвертации. <see cref="Pixels"/> ссылается на внутренний буфер
/// <see cref="CaptureFrameNv12Converter"/> и действителен только до следующего вызова <c>Convert</c>
/// на том же экземпляре (скопируйте в свой буфер, если нужно хранить дольше).
/// </summary>
public readonly struct Nv12ConvertedFrame
{
    public Nv12ConvertedFrame(ReadOnlyMemory<byte> pixels, int width, int height)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
    }

    public ReadOnlyMemory<byte> Pixels { get; }

    public int Width { get; }

    public int Height { get; }
}
