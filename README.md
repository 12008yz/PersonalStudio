# Screen Recorder (Windows)

Репозиторий: **[PersonalStudio](https://github.com/12008yz/PersonalStudio)**.

Десктопный рекордер экрана с системным звуком и микрофоном. **Целевой выход:** стабильный **MP4 (H.264 + AAC-LC)** без FFmpeg в поставке и с понятными ошибками (см. чеклист — реализация по фазам). По опыту — проще «тяжёлых» рекордеров, без упрощения до игрушки.

Подробный чеклист и фазы работы: [SCREEN_RECORDER_PLAN_TODO.md](SCREEN_RECORDER_PLAN_TODO.md).

## Формат выхода (зафиксировано для v1)

- **Контейнер:** MP4 (расширение `.mp4`).
- **Видео:** H.264 (AVC).
- **Аудио:** AAC-LC.
- **Без FFmpeg:** в поставку не входят бинарники FFmpeg и не используется внешний `ffmpeg.exe`; mux/encode — Windows **Media Foundation**, как в плане.

Единая точка в коде: `ScreenRecorder.RecordingEngine.RecordingOutputFormat`.

## Источники записи (зафиксировано для MVP)

- **Видео:** один **выбранный монитор целиком** (`Windows.Graphics.Capture`). Произвольная область экрана — запланировано **после MVP**.
- **Системный звук:** WASAPI **loopback**.
- **Микрофон:** WASAPI **capture** (устройство по умолчанию или выбор в настройках).

В коде: `ScreenRecorder.RecordingEngine.RecordingSourcesSpec`.

## Локализация UI

- **Языки:** **English (en-US)** по умолчанию в проекте и **Russian (ru-RU)**.
- **Файлы:** `src/ScreenRecorder.App/Strings/<lang>/Resources.resw` (MRT / PRI).
- **Использование:** `x:Uid` в XAML, `ResourceLoader` в коде для `Window.Title`, `ms-resource:///Resources/...` в `Package.appxmanifest` для отображаемого имени приложения. Фактический язык — по настройкам Windows для приложения.
- **Важно:** новые ключи добавляйте **сразу в оба** `.resw` (`en-US` и `ru-RU`), иначе возможен английский fallback или пустые подписи.

## Поддерживаемые платформы

- **ОС:** Windows 10 **22H2 и новее** (сборка **19045+**), Windows 11.
- **Архитектура (целевая для v1):** **x64**. В решении также остаются профили x86/ARM64 у шаблона WinUI; для релиза v1 зафиксируем матрицу отдельно.

**Фиксация в артефактах:** минимальная версия для установки/запуска упакованного приложения — `10.0.19045.0` в `src/ScreenRecorder.App/Package.appxmanifest` и `TargetPlatformMinVersion` в `src/ScreenRecorder.App/ScreenRecorder.App.csproj`. Библиотека `ScreenRecorder.RecordingEngine` использует `net8.0-windows10.0.19041.0` (допустимый для SDK диапазон API); это не понижает продуктовый минимум ниже 22H2 — его задаёт манифест приложения.

## Сборка

Требуется .NET 8 SDK и рабочая нагрузка для разработки классических приложений Windows / WinUI (Visual Studio или соответствующие компоненты).

```text
dotnet build ScreenRecorder.slnx -c Release
```

Запуск отладочной упакованной сборки: из каталога `src/ScreenRecorder.App` — `dotnet run` (поддержка через `Microsoft.Windows.SDK.BuildTools.WinApp`).

## Структура

- `src/ScreenRecorder.App` — WinUI 3, только UI и сценарии.
- `src/ScreenRecorder.RecordingEngine` — ядро записи без зависимостей от UI.
