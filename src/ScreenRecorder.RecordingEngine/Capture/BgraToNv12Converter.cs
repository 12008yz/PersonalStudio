using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>
/// CPU-конвертация BGRA8 → NV12 (BT.601 full range) для входа H.264 MFT.
/// Для HD-захвата экрана позже возможен переход на BT.709; stride NV12 — <c>width</c> (согласовано с <see cref="MediaFoundation.Mp4SinkWriterMediaTypes.CreateNv12InputType"/>).
/// </summary>
public static class BgraToNv12Converter
{
    public static void Convert(
        ReadOnlySpan<byte> bgra,
        int width,
        int height,
        int bgraStride,
        Span<byte> nv12)
    {
        if ((width & 1) != 0 || (height & 1) != 0)
            throw new ArgumentException("Width and height must be even for NV12.", nameof(width));

        var expectedBytes = Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(width, height);
        if (nv12.Length < expectedBytes)
        {
            throw new ArgumentException(
                $"NV12 buffer length {nv12.Length} is smaller than required {expectedBytes} for {width}x{height}.",
                nameof(nv12));
        }

        if (bgraStride < width * 4)
            throw new ArgumentOutOfRangeException(nameof(bgraStride), "Stride must cover at least width * 4 bytes per row.");

        var minBgraBytes = bgraStride * (height - 1) + width * 4;
        if (bgra.Length < minBgraBytes)
        {
            throw new ArgumentException(
                $"BGRA buffer length {bgra.Length} is smaller than required {minBgraBytes} for the given stride and size.",
                nameof(bgra));
        }

        var ySize = width * height;
        var yPlane = nv12[..ySize];
        var uvPlane = nv12[ySize..expectedBytes];

        for (var row = 0; row < height; row += 2)
        {
            var bgraRow0 = bgra.Slice(row * bgraStride, bgraStride);
            var bgraRow1 = bgra.Slice((row + 1) * bgraStride, bgraStride);
            var yRow0 = row * width;
            var yRow1 = (row + 1) * width;
            var uvRow = (row / 2) * width;

            for (var col = 0; col < width; col += 2)
            {
                var px = col * 4;
                SampleBgra(bgraRow0, px, out var r00, out var g00, out var b00);
                SampleBgra(bgraRow0, px + 4, out var r01, out var g01, out var b01);
                SampleBgra(bgraRow1, px, out var r10, out var g10, out var b10);
                SampleBgra(bgraRow1, px + 4, out var r11, out var g11, out var b11);

                RgbToYuv(r00, g00, b00, out var y00, out var u00, out var v00);
                RgbToYuv(r01, g01, b01, out var y01, out var u01, out var v01);
                RgbToYuv(r10, g10, b10, out var y10, out var u10, out var v10);
                RgbToYuv(r11, g11, b11, out var y11, out var u11, out var v11);

                yPlane[yRow0 + col] = y00;
                yPlane[yRow0 + col + 1] = y01;
                yPlane[yRow1 + col] = y10;
                yPlane[yRow1 + col + 1] = y11;

                var uvIndex = uvRow + col;
                uvPlane[uvIndex] = (byte)((u00 + u01 + u10 + u11 + 2) >> 2);
                uvPlane[uvIndex + 1] = (byte)((v00 + v01 + v10 + v11 + 2) >> 2);
            }
        }
    }

    private static void SampleBgra(ReadOnlySpan<byte> row, int offset, out byte r, out byte g, out byte b)
    {
        b = row[offset];
        g = row[offset + 1];
        r = row[offset + 2];
    }

    /// <summary>BT.601 full-range 8-bit (0–255).</summary>
    private static void RgbToYuv(byte r, byte g, byte b, out byte y, out byte u, out byte v)
    {
        var yi = (77 * r + 150 * g + 29 * b + 128) >> 8;
        var ui = ((-43 * r - 85 * g + 128 * b + 128) >> 8) + 128;
        var vi = ((128 * r - 107 * g - 21 * b + 128) >> 8) + 128;
        y = (byte)Math.Clamp(yi, 0, 255);
        u = (byte)Math.Clamp(ui, 0, 255);
        v = (byte)Math.Clamp(vi, 0, 255);
    }
}
