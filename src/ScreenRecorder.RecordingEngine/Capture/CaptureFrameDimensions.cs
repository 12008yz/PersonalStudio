using Windows.Graphics;

namespace ScreenRecorder.RecordingEngine.Capture;

/// <summary>Размеры кадра с учётом требований NV12/H.264 (чётные ширина и высота).</summary>
internal static class CaptureFrameDimensions
{
    public static SizeInt32 TruncateToEven(SizeInt32 contentSize)
    {
        var width = contentSize.Width & ~1;
        var height = contentSize.Height & ~1;
        return new SizeInt32(width, height);
    }

    public static void ValidateEvenMinimum(SizeInt32 evenSize)
    {
        if (evenSize.Width < 2 || evenSize.Height < 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(evenSize),
                $"Content size must be at least 2x2 after even rounding; got {evenSize.Width}x{evenSize.Height}.");
        }
    }
}
