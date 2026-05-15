namespace ScreenRecorder.RecordingEngine.MediaFoundation;

/// <summary>Заполнение NV12 однородным цветом (для синтетических кадров и тестов).</summary>
internal static class Nv12SolidColorBuffer
{
    public static byte[] Create(int width, int height, byte y, byte u, byte v)
    {
        var buffer = new byte[Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(width, height)];
        Fill(buffer, width, height, y, u, v);
        return buffer;
    }

    public static void Fill(Span<byte> buffer, int width, int height, byte y, byte u, byte v)
    {
        var ySize = width * height;
        if (buffer.Length < ySize + (width * height / 2))
            throw new ArgumentException("NV12 buffer is too small.", nameof(buffer));

        buffer[..ySize].Fill(y);
        var uv = buffer[ySize..];
        for (var i = 0; i < uv.Length; i += 2)
        {
            uv[i] = u;
            uv[i + 1] = v;
        }
    }
}
