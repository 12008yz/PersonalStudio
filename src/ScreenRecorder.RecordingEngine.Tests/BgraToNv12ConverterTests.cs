using ScreenRecorder.RecordingEngine.Capture;
using ScreenRecorder.RecordingEngine.MediaFoundation;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class BgraToNv12ConverterTests
{
    [TestMethod]
    public void Convert_solid_red_fills_expected_y_and_chroma()
    {
        const int width = 4;
        const int height = 4;
        var bgra = CreateSolidBgra(width, height, b: 0, g: 0, r: 255, a: 255);
        var nv12 = new byte[Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(width, height)];

        BgraToNv12Converter.Convert(bgra, width, height, width * 4, nv12);

        var ySize = width * height;
        Assert.IsTrue(nv12.AsSpan(0, ySize).ToArray().All(y => y == 77));
        Assert.IsTrue(nv12.AsSpan(ySize).ToArray().All(b => b == 85 || b == 255));
        Assert.AreEqual((byte)85, nv12[ySize]);
        Assert.AreEqual((byte)255, nv12[ySize + 1]);
    }

    [TestMethod]
    public void Convert_solid_black_y_is_zero_chroma_centered()
    {
        const int width = 2;
        const int height = 2;
        var bgra = CreateSolidBgra(width, height, 0, 0, 0, 255);
        var nv12 = new byte[Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(width, height)];

        BgraToNv12Converter.Convert(bgra, width, height, width * 4, nv12);

        Assert.AreEqual(0, nv12[0]);
        Assert.AreEqual(0, nv12[1]);
        Assert.AreEqual((byte)128, nv12[width * height]);
        Assert.AreEqual((byte)128, nv12[width * height + 1]);
    }

    [TestMethod]
    public void Convert_honors_bgra_stride_wider_than_width()
    {
        const int width = 4;
        const int height = 2;
        const int bgraStride = 32;
        var bgra = new byte[bgraStride * height];
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var o = row * bgraStride + col * 4;
                bgra[o] = 0;
                bgra[o + 1] = 0;
                bgra[o + 2] = 255;
                bgra[o + 3] = 255;
            }
        }

        var nv12 = new byte[Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(width, height)];
        BgraToNv12Converter.Convert(bgra, width, height, bgraStride, nv12);

        Assert.IsTrue(nv12.AsSpan(0, width * height).ToArray().All(y => y == 77));
    }

    [TestMethod]
    public void Convert_rejects_odd_width()
    {
        var bgra = new byte[3 * 2 * 4];
        var nv12 = new byte[Mp4SinkWriterMediaTypes.CalculateNv12BufferSize(2, 2)];

        Assert.ThrowsException<ArgumentException>(() =>
            BgraToNv12Converter.Convert(bgra, 3, 2, 12, nv12));
    }

    [TestMethod]
    public void TruncateToEven_rounds_down_odd_dimensions()
    {
        var even = CaptureFrameDimensions.TruncateToEven(new Windows.Graphics.SizeInt32 { Width = 1919, Height = 1079 });
        Assert.AreEqual(1918, even.Width);
        Assert.AreEqual(1078, even.Height);
    }

    private static byte[] CreateSolidBgra(int width, int height, byte b, byte g, byte r, byte a)
    {
        var stride = width * 4;
        var pixels = new byte[stride * height];
        for (var row = 0; row < height; row++)
        {
            for (var col = 0; col < width; col++)
            {
                var o = row * stride + col * 4;
                pixels[o] = b;
                pixels[o + 1] = g;
                pixels[o + 2] = r;
                pixels[o + 3] = a;
            }
        }

        return pixels;
    }
}
