using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;

// Вариант A плана: синтетическое видео → валидный MP4 на целевой ОС (без FFmpeg).
// WinRT MediaComposition (под капотом Media Foundation). Явного AAC в коде нет — см. README проекта.
// IMFSinkWriter — в RecordingEngine (фаза D).

var tempFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetTempPath());
var fileName = "ScreenRecorderSpike_" + Guid.NewGuid().ToString("n") + ".mp4";
var file = await tempFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

var gray = Windows.UI.Color.FromArgb(255, 90, 90, 90);
var clip = MediaClip.CreateFromColor(gray, TimeSpan.FromSeconds(2));
var composition = new MediaComposition();
composition.Clips.Add(clip);

var enc = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p);
await composition.RenderToFileAsync(file, MediaTrimmingPreference.Fast, enc);

Console.WriteLine(file.Path);
