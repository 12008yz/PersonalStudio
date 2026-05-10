# Screen Recorder (Windows)

Репозиторий: **[PersonalStudio](https://github.com/12008yz/PersonalStudio)**.

Десктопный рекордер экрана с системным звуком и микрофоном. **Целевой выход:** стабильный **MP4 (H.264 + AAC-LC)** без FFmpeg в поставке и с понятными ошибками (см. чеклист — реализация по фазам). По опыту — проще «тяжёлых» рекордеров, без упрощения до игрушки.

Подробный чеклист и фазы работы: [SCREEN_RECORDER_PLAN_TODO.md](SCREEN_RECORDER_PLAN_TODO.md).

## Запись, закон и этика

Это **не юридическая консультация** и **не обещание** соответствия каким‑либо нормам. Законы и правила (в т.ч. о персональных данных, тайне связи, авторских правах, трудовом праве) **различаются по странам и ситуациям** — разбирайтесь **самостоятельно** или с юристом.

- Запись **экрана, звука и голосов других людей** без их **ведома и согласия** там, где это требуется, может быть **незаконной** или нарушать политику работодателя/площадки.
- Уважайте **конфиденциальность**, **коммерческую тайну** и условия сервисов; контент с **DRM** или защитой может **не записываться** или отображаться иначе — это ожидаемо.
- Используйте инструмент **законно и этично**; авторы репозитория **не несут ответственности** за ваши действия.

В продукте планируется **явная индикация записи** и отказ от сценариев скрытой съёмки (см. план, фаза качества).

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

## Нефункциональные требования (v1)

Ориентиры для приёмки и регрессии (не юридические гарантии):

- **Референсный сценарий:** вывод/захват до **1920×1080**, **30 fps** на типичном ноутбуке (см. план). MVP по-прежнему пишет **весь выбранный монитор**; при **1440p/4K** нагрузка выше эталона — отдельные целевые пороги можно задать позже.
- **Дропы кадров:** на референсе доля пропусков на скользящем окне **60 с** — не выше **5%** (`RecordingNfrSpec.MaxFrameDropRatioPerRolling60Seconds`).
- **A/V:** на контрольных клипах ориентир по модулю рассинхрона **±100 ms** (`RecordingNfrSpec.AcceptableAvDriftMilliseconds`), уточняется после появления пайплайна.
- **Сбои:** явные сообщения пользователю (`RecordingNfrSpec.SurfaceFailuresToUser`), по возможности контролируемая остановка и финализация файла.

Единая точка в коде: `ScreenRecorder.RecordingEngine.RecordingNfrSpec`.

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

Сборка всего решения проходит **без** `-p:Platform=…` (это нормальная конфигурация по умолчанию для SDK-style проектов). Явное сочетание `ScreenRecorder.slnx` + `-p:Platform=x64` в текущем виде файла решения может дать **MSB4126** («недопустимая конфигурация») — в таком случае собирайте проект приложения напрямую:

```text
dotnet build src/ScreenRecorder.App/ScreenRecorder.App.csproj -c Release -p:Platform=x64
```

Запуск отладочной упакованной сборки: из каталога `src/ScreenRecorder.App` — `dotnet run -p:Platform=x64` (или без `Platform`, если так настроен ваш шаблон; поддержка через `Microsoft.Windows.SDK.BuildTools.WinApp`).

Для WinUI в привычном inner-loop часто указывают `-p:Platform=x64` (или `x86` / `ARM64`) **на уровне `.csproj`**, а не `.slnx` с явной платформой.

## Тесты

Модульные тесты ядра (`ScreenRecorder.RecordingEngine.Tests`, MSTest):

```text
dotnet test src/ScreenRecorder.RecordingEngine.Tests/ScreenRecorder.RecordingEngine.Tests.csproj -c Debug -p:Platform=x64
```

Не используйте `dotnet test ... --no-build`, если перед этим тестовый проект не собирали с **тем же** `Configuration` / `Platform` — иначе тестовая сборка может не находиться по пути.

Часть проверок завязана на интерактивную Windows-сессию с мониторами (перечисление дисплеев).

## Захват экрана (ограничения на этапе WGC)

- **DRM и защищённый контент:** окна или области с защитой от копирования могут давать **чёрный кадр** или пустой вывод — это ограничение ОС/политики контента, а не «сломанный код».
- **Смена разрешения и масштаба:** при изменении режима дисплея во время захвата пайплайн должен **пересоздавать** пул кадров (`Direct3D11CaptureFramePool.Recreate`); в движке это учтено по `ContentSize` кадра. Полные сценарии и UX-сообщения — по [SCREEN_RECORDER_PLAN_TODO.md](SCREEN_RECORDER_PLAN_TODO.md) (фаза B).
- **DPI:** в приложении заявлен **PerMonitorV2** (`app.manifest`); регрессия на 125% / 150% — в чеклисте плана.

## Структура

- `src/ScreenRecorder.App` — WinUI 3, только UI и сценарии.
- `src/ScreenRecorder.RecordingEngine` — ядро записи без зависимостей от UI.
- `src/ScreenRecorder.RecordingEngine.Tests` — модульные тесты для `RecordingEngine`.
- `src/ScreenRecorder.MfSpike` — спайк A: синтетическое видео → MP4 без FFmpeg (см. [src/ScreenRecorder.MfSpike/README.md](src/ScreenRecorder.MfSpike/README.md)).
- `src/ScreenRecorder.VariantBSpike` — спайк B: GDI **5 с** + синус в **WAV** → один MP4 (аудио на выходе обычно **AAC**; см. [src/ScreenRecorder.VariantBSpike/README.md](src/ScreenRecorder.VariantBSpike/README.md)).
- `docs/HARDWARE_CODEC_MATRIX.md` — таблица ручной фиксации результатов спайков на разных GPU (построчно по ПК).
