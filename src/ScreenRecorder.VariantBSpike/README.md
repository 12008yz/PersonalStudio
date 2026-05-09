# ScreenRecorder.VariantBSpike

**Вариант B** из плана: **5 с реального захвата содержимого основного монитора** + **синтетическое аудио** (синус **440 Hz** в **WAV** PCM 48 kHz stereo) → один **MP4** через `MediaComposition` (видео обычно **H.264**; аудио на выходе обычно **AAC**, часто **AAC-LC** — смотрите свойства файла / mediainfo), **без FFmpeg**.

## Как устроено

- **Видео:** `Graphics.CopyFromScreen` по области **PrimaryScreen** (GDI), кадр каждые **200 ms** → **5 s** = **25 JPEG** (разрешение = размер основного монитора, не обязательно 1920×1080) → цепочка **`MediaClip.CreateFromImageFileAsync(..., frameDuration)`**. Профиль рендера **`HD1080p`** — целевое качество контейнера; фактическое масштабирование задаёт стек ОС.
- **Аудио:** PCM **16-bit**, **48 kHz**, стерео, длительность **5 s** → **`BackgroundAudioTrack.CreateFromFileAsync`**.
- **Сборка:** скрытая **WinForms**-форма (STA + цикл сообщений для WinRT).

Это **не** DXGI / `Windows.Graphics.Capture` (они пойдут в фазу захвата продукта), но закрывает смысл спайка: **реальные пиксели экрана + звук в одном MP4**.

## Запуск

```text
dotnet run --project src/ScreenRecorder.VariantBSpike/ScreenRecorder.VariantBSpike.csproj -c Release
```

В stdout — путь к `.mp4` в `%TEMP%`. Временные JPEG и WAV после рендера удаляются.

## Требования

- Windows как у основного приложения; **.NET 8**; **Windows App SDK** из `csproj`.
