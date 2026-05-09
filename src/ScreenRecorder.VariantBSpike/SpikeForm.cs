using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace ScreenRecorder.VariantBSpike;

/// <summary>
/// Скрытая форма: нужен STA-поток и цикл сообщений для стабильной работы WinRT при GDI/Media API.
/// </summary>
internal sealed class SpikeForm : Form
{
    public SpikeForm()
    {
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        Opacity = 0;
        Size = new Size(1, 1);
        Load += async (_, _) => await RunSpikeThenExitAsync();
    }

    private async Task RunSpikeThenExitAsync()
    {
        try
        {
            await SpikeRunner.RunAsync();
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            Environment.Exit(1);
        }
    }
}

internal static class SpikeRunner
{
    private const int CaptureDurationSeconds = 5;
    private const int FrameIntervalMs = 200;
    private const double SineFrequencyHz = 440;

    public static async Task RunAsync()
    {
        var temp = await StorageFolder.GetFolderFromPathAsync(Path.GetTempPath());
        var sessionId = Guid.NewGuid().ToString("n");
        var framesFolder = await temp.CreateFolderAsync("ScreenRecorderVariantB_" + sessionId, CreationCollisionOption.ReplaceExisting);

        var bounds = Screen.PrimaryScreen?.Bounds
            ?? throw new InvalidOperationException("Primary screen not found.");

        var frameCount = (CaptureDurationSeconds * 1000) / FrameIntervalMs;
        var frameDuration = TimeSpan.FromMilliseconds(FrameIntervalMs);

        for (var i = 0; i < frameCount; i++)
        {
            using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            }

            var file = await framesFolder.CreateFileAsync($"f{i:D4}.jpg", CreationCollisionOption.ReplaceExisting);
            using (var ras = await file.OpenAsync(FileAccessMode.ReadWrite))
            using (var net = ras.AsStream())
            {
                bmp.Save(net, ImageFormat.Jpeg);
            }
        }

        var wavFile = await WriteSineWavAsync(temp, sessionId, CaptureDurationSeconds);

        var composition = new MediaComposition();
        for (var i = 0; i < frameCount; i++)
        {
            var img = await framesFolder.GetFileAsync($"f{i:D4}.jpg");
            var clip = await MediaClip.CreateFromImageFileAsync(img, frameDuration);
            composition.Clips.Add(clip);
        }

        var bg = await BackgroundAudioTrack.CreateFromFileAsync(wavFile);
        composition.BackgroundAudioTracks.Add(bg);

        var outFile = await temp.CreateFileAsync($"ScreenRecorderVariantB_{sessionId}.mp4", CreationCollisionOption.ReplaceExisting);
        var enc = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
        await composition.RenderToFileAsync(outFile, MediaTrimmingPreference.Fast, enc);

        Console.WriteLine(outFile.Path);

        try
        {
            await wavFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
        catch
        {
            /* ignore */
        }

        try
        {
            await framesFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);
        }
        catch
        {
            /* ignore */
        }
    }

    private static async Task<StorageFile> WriteSineWavAsync(StorageFolder temp, string sessionId, int seconds)
    {
        const int sampleRate = 48000;
        const short channels = 2;
        const short bits = 16;
        var numSamples = sampleRate * seconds * channels;
        var data = new short[numSamples];
        for (var i = 0; i < numSamples; i += channels)
        {
            var t = (double)(i / channels) / sampleRate;
            var v = (short)(short.MaxValue * 0.2 * Math.Sin(2 * Math.PI * SineFrequencyHz * t));
            data[i] = v;
            data[i + 1] = v;
        }

        var dataBytes = MemoryMarshal.AsBytes(data.AsSpan()).ToArray();
        var file = await temp.CreateFileAsync($"ScreenRecorderVariantB_{sessionId}.wav", CreationCollisionOption.ReplaceExisting);
        using (var s = await file.OpenAsync(FileAccessMode.ReadWrite))
        using (var bw = new BinaryWriter(s.AsStream()))
        {
            var blockAlign = (short)(channels * (bits / 8));
            var byteRate = sampleRate * blockAlign;
            var dataChunkSize = dataBytes.Length;

            bw.Write("RIFF"u8.ToArray());
            bw.Write(36 + dataChunkSize);
            bw.Write("WAVE"u8.ToArray());
            bw.Write("fmt "u8.ToArray());
            bw.Write(16);
            bw.Write((short)1);
            bw.Write(channels);
            bw.Write(sampleRate);
            bw.Write(byteRate);
            bw.Write(blockAlign);
            bw.Write(bits);
            bw.Write("data"u8.ToArray());
            bw.Write(dataChunkSize);
            bw.Write(dataBytes);
            bw.Flush();
        }

        return file;
    }
}
