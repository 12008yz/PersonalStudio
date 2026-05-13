using NAudio.Wave;
using ScreenRecorder.RecordingEngine.Audio;

namespace ScreenRecorder.RecordingEngine.Tests;

[TestClass]
public sealed class AppendOnlyPcmWaveFileWriterTests
{
    [TestMethod]
    public void Append_IeeeFloatStereo_WrittenFileMatchesDuration()
    {
        var path = Path.Combine(Path.GetTempPath(), "ScreenRecorderAppendOnlyWavTest_" + Guid.NewGuid().ToString("n") + ".wav");
        try
        {
            var fmt = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
            var oneFrame = new byte[fmt.BlockAlign]; // 1 sample frame stereo float

            using (var w = new AppendOnlyPcmWaveFileWriter(path))
            {
                w.Append(oneFrame, fmt);
                w.Append(oneFrame, fmt);
            }

            using var reader = new WaveFileReader(path);
            Assert.AreEqual(fmt.SampleRate, reader.WaveFormat.SampleRate);
            Assert.AreEqual(fmt.Channels, reader.WaveFormat.Channels);
            Assert.AreEqual(2L * oneFrame.Length, reader.Length);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    [TestMethod]
    public void Dispose_WithoutAppend_DoesNotCreateFile()
    {
        var path = Path.Combine(Path.GetTempPath(), "ScreenRecorderAppendOnlyWavEmpty_" + Guid.NewGuid().ToString("n") + ".wav");
        using (new AppendOnlyPcmWaveFileWriter(path))
        {
        }

        Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void Append_SecondChunkWithDifferentSampleRate_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), "ScreenRecorderAppendOnlyWavBad_" + Guid.NewGuid().ToString("n") + ".wav");
        try
        {
            var fmt48 = WaveFormat.CreateIeeeFloatWaveFormat(48_000, 2);
            var fmt44 = WaveFormat.CreateIeeeFloatWaveFormat(44_100, 2);
            var buf = new byte[fmt48.BlockAlign];

            using var w = new AppendOnlyPcmWaveFileWriter(path);
            w.Append(buf, fmt48);
            Assert.ThrowsException<InvalidOperationException>(() => w.Append(buf, fmt44));
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
