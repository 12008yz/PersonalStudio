# ScreenRecorder.MfSpike

Вертикальный спайк из плана (**вариант A**): убедиться, что на Windows целевой конфигурации можно получить **валидный MP4** без FFmpeg.

## Что делает

- Собирает **2 секунды** «видео» — сплошной цвет (`MediaClip.CreateFromColor`).
- Кодирует и упаковывает через **`MediaComposition.RenderToFileAsync`** в **MP4** (`CreateMp4(VideoEncodingQuality.HD1080p)`; видео — системный **H.264**).
- Аудио в этом минимальном сценарии **не задаётся вручную**: дорожка может отсутствовать или быть «тишиной» — смотрите свойства файла в плеере или mediainfo. Отдельная дорожка с синтезированным звуком (вход **PCM WAV**, на выходе MP4 типично **AAC**) — спайк **[`VariantBSpike`](../ScreenRecorder.VariantBSpike/README.md)**; жёсткая спецификация **AAC-LC** в контейнере — фаза движка (**`IMFSinkWriter`**).

Это **не** замена будущему коду на **`IMFSinkWriter`** в `RecordingEngine`, но подтверждает, что стек ОС для **MP4 + видеокодека** на машине работает.

## Запуск

Из корня репозитория:

```text
dotnet run --project src/ScreenRecorder.MfSpike/ScreenRecorder.MfSpike.csproj
```

В stdout выводится полный путь к файлу в `%TEMP%`. Проверьте воспроизведение плеером по умолчанию.

## Требования

- Windows 10 22H2+ / 11 (как у основного приложения).
- .NET 8 SDK, пакет Windows App SDK восстанавливается из `csproj`.
