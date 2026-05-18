# План: запись экрана + системный звук + микрофон (без FFmpeg)

проверь всё за собой ещё раз. что бы не было ошибок или несостыковок в логике.

привет. прочитай файл SCREEN_RECORDER_PLAN_TODO.md и давай выполнять следующий пункт плана

Чеклист по фазам, лучшие практики
и доработки после ревью плана. Отмечай `- [x]` по мере выполнения.

---

## 0. Продукт и границы

**Цель (зафиксировано 2026-05-10):** десктопное приложение для Windows — по опыту проще типичного «тяжёлого» рекордера в духе Bandicam, но без упрощения до «игрушки»: приоритет — **стабильный выходной MP4** и **понятные пользователю ошибки** (права, устройства, кодеки, DRM), а не максимум настроек на первом экране.

**В репозитории:** цель, платформа, формат выхода, источники, локализация, НФТ и **краткое предупреждение о записи (закон/этика)** — в [README.md](README.md), строки UI в `src/ScreenRecorder.App/Strings/`, спеки в `RecordingOutputFormat` / `RecordingSourcesSpec` / `RecordingAudioSpec` / `RecordingAcousticUxSpec` / `RecordingNfrSpec`, спайки MF — [`ScreenRecorder.MfSpike`](src/ScreenRecorder.MfSpike/README.md) и [`ScreenRecorder.VariantBSpike`](src/ScreenRecorder.VariantBSpike/README.md), шаблон матрицы железа — [docs/HARDWARE_CODEC_MATRIX.md](docs/HARDWARE_CODEC_MATRIX.md), **COM/потоки и MF** — [docs/COM_AND_THREADING.md](docs/COM_AND_THREADING.md), **дрейф A/V на длинных сессиях** — [docs/AV_DRIFT_POLICY.md](docs/AV_DRIFT_POLICY.md), **ручной DPI-регресс (125% / 150%)** — [docs/DPI_MANUAL_TEST_CHECKLIST.md](docs/DPI_MANUAL_TEST_CHECKLIST.md); каркас `ScreenRecorder.slnx`, `src/ScreenRecorder.App` (WinUI 3).

- [x] Зафиксировать цель: десктоп «проще Bandicam», но полноценный (стабильный MP4, понятные ошибки).
- [x] ОС: Windows 10 22H2+ и Windows 11 (x64).
- [x] Выход: MP4 (H.264 + AAC-LC), без FFmpeg в составе.

**Формат выхода (зафиксировано):** MP4, видео **H.264**, аудио **AAC-LC**; кодирование/mux — **Media Foundation**, без FFmpeg в поставке. См. `RecordingOutputFormat` в `ScreenRecorder.RecordingEngine` и раздел в [README.md](README.md).

- [x] Источники: экран (**MVP — монитор целиком**; произвольная область — после MVP), loopback, микрофон.

**Источники (зафиксировано для MVP):** видео — **один монитор целиком** (не произвольная область); **системный звук** — WASAPI loopback; **микрофон** — WASAPI. Область экрана и мульти-мониторные сценарии сверх «один выбранный монитор» — после MVP, см. `RecordingSourcesSpec`.

- [x] UI: русский + английский (строки вынести в ресурсы).

**Локализация:** языки **en-US** (по умолчанию) и **ru-RU**; строки в `Strings/<lang>/Resources.resw`, манифест и заголовок окна через **MRT** (`ms-resource:///...`, `x:Uid`, `ResourceLoader`). Язык интерфейса следует языку системы/приложения Windows.

- [x] НФТ: 1080p30 на типичном ноуте без постоянных дропов; измеримая A/V синхронизация; честное поведение при сбоях.

**НФТ (зафиксировано):** референс **1080p @ 30 fps** (MVP пишет весь монитор — на 1440p/4K нагрузка выше эталона); доля дропов на окне **60 с** не выше **5%**; **A/V** — ориентир **±100 ms** на контрольных клипах; при сбоях — явные ошибки и контролируемый stop/finalize. См. `RecordingNfrSpec` и [README.md](README.md).

- [x] README: краткое предупреждение об ограничениях записи по закону/этике (без юридических обещений).

**README (зафиксировано):** раздел **«Запись, закон и этика»** в [README.md](README.md) — дисклеймер, ответственность пользователя, согласие/конфиденциальность, DRM; без юридических гарантий.

---

## Риск: ранний вертикальный спайк Media Foundation (1–3 дня, до больших вложений в capture)

**Цель:** доказать, что на целевых ПК цепочка **MP4 + H.264 (+ при необходимости AAC)** через стек ОС работает (прямой **`IMFSinkWriter`** или эквивалентный путь WinRT поверх **Media Foundation**).

- [x] Вариант A: синтетическое видео → валидный MP4 на целевой ОС (отдельная аудиодорожка в этом спайке **не** задаётся).

**Вариант A (сделано):** консольный проект [`ScreenRecorder.MfSpike`](src/ScreenRecorder.MfSpike/README.md) — **2 с** сплошного цвета → **MP4** (`HD1080p`) через `Windows.Media.Editing.MediaComposition` (под капотом **MF** и системные кодеки). Это проверка **видеопути и контейнера**; аудио может отсутствовать. Отдельная управляемая звуковая дорожка — **вариант B**; явный **AAC-LC** и таймстемпы под движок — **`IMFSinkWriter`**, фаза D.

- [x] Вариант B: реальный захват 5 с + синтетическое аудио → валидный MP4 (на выходе аудио обычно **AAC**, в т.ч. **AAC-LC** — проверить mediainfo).

**Вариант B (сделано):** [`ScreenRecorder.VariantBSpike`](src/ScreenRecorder.VariantBSpike/README.md) — **GDI** `CopyFromScreen` (реальные пиксели основного монитора) **5 с**, **25** кадров по **200 ms** → цепочка image-clips; в коде аудио — **PCM WAV** (синус **48 kHz**) → `BackgroundAudioTrack`; при **RenderToFileAsync** ОС обычно кодирует звук в **AAC** (профиль **LC** не фиксируется API в явном виде). Это не `Windows.Graphics.Capture`, но закрывает спайк «экран + звук в одном файле».

- [x] Зафиксировать HRESULT/отсутствие кодеков на тестовых машинах (Intel / NVIDIA / AMD).

**Шаблон / журнал:** [docs/HARDWARE_CODEC_MATRIX.md](docs/HARDWARE_CODEC_MATRIX.md) — данные по ПК вносить вручную после прогона `MfSpike` и `VariantBSpike`. На **2026-05-10** заполнена одна строка (гибрид NVIDIA+Intel); отдельные конфигурации **AMD** и только **Intel iGPU** — по мере доступа к тестовым машинам.

---

## 1. Стек (зафиксировать на старте)

- [x] .NET: одна LTS-ветка на весь проект (не прыгать между major без причины). _(основной стек: **.NET 8** — App, RecordingEngine, спайки.)_
- [x] UI: WinUI 3 + Windows App SDK (stable).
- [x] Захват экрана: Windows.Graphics.Capture (основной путь); DXGI Duplication — только при блокерах. _(WGC + D3D11 в `RecordingEngine.Capture`, тест — кнопка в App; DXGI не делали.)_
- [x] Кодирование/контейнер: Media Foundation (`IMFSinkWriter`, H.264 MFT, AAC MFT). _(в `RecordingEngine` подключён **Vortice.MediaFoundation** + `MediaFoundationLifetime` / `MediaFoundationEncoderCatalog` — перечень энкодеров H.264 и AAC; **`IMFSinkWriter`** и запись MP4 — фаза D.)_
- [x] Аудио: WASAPI (+ удобный слой, напр. NAudio); единая номинальная частота (предпочтительно 48 kHz). _(микрофон `MicrophoneCaptureSession`, loopback `LoopbackCaptureSession`, номинал `RecordingAudioSpec.NominalSampleRateHz`.)_
- [x] Логи: `Microsoft.Extensions.Logging` (debug подробно, release без PII). _(подключено в App; политика release — донастроить в фазе F.)_
- [x] Конфиг: `%LocalAppData%\<AppId>\settings.json` + валидация. _(см. `ApplicationIdentity` + `JsonAppSettingsStore` в RecordingEngine.)_
- [x] Ядро записи: отдельный модуль/сервис с жизненным циклом и `CancellationToken` (не глобальные «магические» синглтоны на всё).

---

## 2. Архитектура

- [x] Слой UI — только команды, настройки, состояние; без тяжёлых циклов на UI-потоке. _(каркас; тяжёлых циклов нет.)_
- [x] `RecordingSession` / Orchestrator — единственный дирижёр: Start/Stop, смена устройств.
- [x] Видеопайплайн — кадры с монотонных часов (QPC) и метки времени. _(в `MonitorFrameCaptureSession`: QPC + `SystemRelativeTime`; без очереди под энкодер — фаза D.)_
- [x] Аудиопайплайн — loopback + mic → раздельные PCM-потоки с общим контрактом; **в MP4 для MVP** — одна смешанная AAC-LC дорожка (`RecordingAudioSpec.MvpMp4AudioTrackLayout`). _(контракт `SourcedPcmCaptureDataAvailableEventArgs` + `PcmCaptureSourceKind`; агрегирующее событие `MicAndLoopbackCaptureSession.PcmDataAvailable`; две отдельные AAC-дорожки — только `Mp4AudioTrackLayout.DualSeparateSystemAndMicrophoneAacLc`, v1.1.)_
- [x] Encoder/Muxer (MF) — отдельный поток/очередь с **ограничением размера** (backpressure). _(инфраструктура: `BoundedEncoderWorkQueue<TWorkItem>` в `MediaFoundation`.)_
- [x] Device layer — мониторы, аудиоустройства, «устройство отключили». _(добавлен `IDeviceTopologyMonitor`/`PollingDeviceTopologyMonitor`: снапшоты мониторов + capture/render endpoints, детекция removal/default-change через событие `TopologyChanged`.)_
- [x] **COM/потоки:** явно описать, какие потоки MTA/STA, где `CoInitializeEx`, где живёт SinkWriter; не вызывать MF с UI-потока. _(см. [docs/COM_AND_THREADING.md](docs/COM_AND_THREADING.md).)_
- [x] **Дрейф A/V:** политика на длинных записях (30–120+ мин) — resample аудио / редкий drop-дубликат видео / пересчёт таймстемпов. _(см. [docs/AV_DRIFT_POLICY.md](docs/AV_DRIFT_POLICY.md); пороги наблюдения — `RecordingNfrSpec.LongContinuousRecordingMinutesThreshold` / `AvDriftObservationIntervalSeconds`.)_

---

## Фаза A — каркас (неделя 1)

- [x] Решение: UI-проект + Class Library `RecordingEngine` (без зависимостей от UI).
- [x] Пакеты: Windows App SDK, CsWinRT (нужные WinRT API), NAudio (или свой тонкий слой). _(NAudio в `RecordingEngine`; WASDK/CsWinRT в App/SDK.)_
- [x] Версионирование: `AssemblyInformationalVersion`, единый AppId. _(версия через корневой `Directory.Build.props`; папка настроек — `ApplicationIdentity.LocalAppDataFolderName`.)_
- [x] `EditorConfig`, nullable, warnings-as-errors для `RecordingEngine` (минимум).
- [x] DI (`Microsoft.Extensions.DependencyInjection`): сессия, логгер, настройки. _(DI + лог + `IAppSettingsStore`; сессия записи — фаза E.)_
- [x] Модульные тесты `RecordingEngine` (MSTest): `FrameCaptureMetrics`, перечисление мониторов. См. `src/ScreenRecorder.RecordingEngine.Tests`.
- [x] **Готово:** пустое окно, лог, чтение/запись настроек.

---

## Фаза B — захват экрана без кодирования (неделя 1–2)

- [x] Перечисление дисплеев + связка с `GraphicsCaptureItem`.
- [x] `GraphicsCaptureSession` + Direct3D11 interop: стабильный поток кадров + timestamp.
- [x] Debug: FPS и учёт «пустых» кадров (`FrameCaptureMetrics`).
- [x] Debug: средняя задержка кадра относительно `Direct3D11CaptureFrame.SystemRelativeTime` после `TryGetNextFrame` — среднее и последнее в миллисекундах (`FrameCaptureMetrics`, журнал после теста захвата); кадр успешного `Recreate` в выборку латентности не входит, базовая QPC-метка сбрасывается.
- [x] Ошибки: права, конфликт захвата, смена разрешения/масштаба. _(`Recreate` по `ContentSize`; счётчик `FrameCaptureMetrics.PoolRecreateFailureCount` + предупреждение в тесте захвата; `ScreenCaptureFailureClassifier` + строки `CaptureError_\*`/`CaptureTest*PoolRecreateFailures` в UI.)*
- [x] DPI: PerMonitorV2, тест 125% / 150%. _(PerMonitorV2 в `src/ScreenRecorder.App/app.manifest`; пошаговый ручной регресс — [docs/DPI_MANUAL_TEST_CHECKLIST.md](docs/DPI_MANUAL_TEST_CHECKLIST.md).)_
- [x] **Готово:** 60 с захвата без утечки VRAM/RAM (диспетчер задач). _(60s test: WorkingSet/PrivateBytes держатся на плато, тест не падает; GPU utilization не поднималась выше ~0.1.)_
- [x] **Ограничение:** зафиксировать в UX/доках возможный **чёрный экран** на DRM/защищённом контенте (ожидаемо). _(раздел в [README.md](README.md) «Захват экрана (ограничения)».)_

---

## Фаза C — аудио (неделя 2–3)

- [x] Перечисление устройств (`MMDeviceEnumerator` через NAudio Core Audio); UI выбора default/конкретного _(ComboBox на `MainPage`, сохранение в `AppSettings` / JSON). Реальный WASAPI захват по выбранным id — следующие пункты._
- [x] Микрофон: WASAPI shared → PCM; формат (частота, каналы). _(см. `MicrophoneCaptureSession` + `RecordingAudioSpec.NominalSampleRateHz`; фактический формат — от shared-микшера, целевая 48 kHz — следующий пункт ресэмплинга.)_
- [x] Системный звук: WASAPI loopback. _(см. `LoopbackCaptureSession`, `RenderEndpointMmDevice`; буферы только при активном воспроизведении на выбранном выводе.)_
- [x] Ресэмплинг к 48 kHz (или одна выбранная частота на весь проект).
- [x] **Продуктовое решение:** одна микшированная дорожка **или** две AAC-дорожки в MP4 (v1 vs v1.1 — зафиксировать). _(MVP / v1: **одна** смешанная стерео AAC-LC — `RecordingAudioSpec.MvpMp4AudioTrackLayout` = `SingleMixedStereoAacLc`; две дорожки — `DualSeparateSystemAndMicrophoneAacLc`, возможная v1.1; README + `RecordingOutputFormat`.)_
- [x] **Акустика:** риск гула без наушников; подсказка в UI; мониторинг выкл по умолчанию; опционально ducking. _(блок предупреждения + переключатель «мониторинг» на `MainPage`, `AppSettings.AudioPassthroughMonitoringEnabled` по умолчанию false; `RecordingAcousticUxSpec` — ducking не в MVP; воспроизведение в динамики при записи — фаза E.)_
- [x] **Смена default audio** во время записи: политика (переподключение / стоп / ошибка). _(MVP: если в UI выбрано «По умолчанию» (`null` id), при `DefaultCaptureEndpointChanged` / `DefaultRenderEndpointChanged` перезапуск соответствующей ноги WASAPI — `RecordingAudioDefaultDevicePolicy` + `MicAndLoopbackCaptureSession.Restart*`; явный выбор устройства не трогаем; сбой перезапуска — фатальная ошибка записи без автоповторов.)_
- [x] Тест: 2 мин (например YouTube + голос), клиппинг, рассинхрон. _(Кнопки «10 с» и «2 мин» на `MainPage` + лог: байты, счётчики IEEE float |U|≥1, оценка Δ длительности PCM, **WAV-дампы** в `%TEMP%`; прогресс 30/60/90 с только для 2 мин; ручной чеклист — [docs/AUDIO_2MIN_MANUAL_TEST_CHECKLIST.md](docs/AUDIO_2MIN_MANUAL_TEST_CHECKLIST.md); полная A/V после mux — фаза D.)_
- [x] **Готово:** временные WAV-дампы звучат корректно до видеокодека.

---

## Фаза D — Media Foundation → MP4 (неделя 3–5)

- [x] `MFStartup`, проверка H.264 encoder MFT и AAC encoder MFT на целевых ПК. _( `MediaFoundationLifetime` / `MediaFoundationEncoderCatalog` / `MediaFoundationEncoderAvailability.Probe()` + тесты; матрица железа [docs/HARDWARE_CODEC_MATRIX.md](docs/HARDWARE_CODEC_MATRIX.md) — дополнять на AMD / «чистый» Intel iGPU по мере тестов.)_
- [x] `IMFSinkWriter` → `.mp4`: видео H.264, аудио AAC-LC, битрейты разумные. _(базовый mux: `Mp4SinkWriter` + `Mp4SinkWriterConfiguration`, NV12/PCM16 → MP4, тесты `Mp4SinkWriterTests` / `Mp4SinkWriterMediaTypesTests`; **не закрывает** фазу D: конвертация из захвата, time origin QPC, GOP, finalize при ошибках, матрица GPU — ниже.)_
- [x] Конверсия кадра в формат энкодера (часто NV12): сначала CPU, потом оптимизация (шейдер). _(CPU: `BgraToNv12Converter`, readback `Direct3D11BgraFrameReader`, фасад `CaptureFrameNv12Converter`; шейдер — позже.)_
- [x] Общий time origin при старте сессии; согласование видео QPC и аудио-клока. _(`RecordingSessionTimebase` + привязка в `RecordingRuntime`; видео — `CapturedVideoFrameEventArgs` / QPC−latency; аудио — счётчики сэмплов на ногах mic/loopback в `SourcedPcmCaptureDataAvailableEventArgs`; тесты `RecordingSessionTimebaseTests`.)_
- [x] GOP / keyframe interval; CBR/VBR — выбрать и протестировать. _(MVP: **peak-constrained VBR** (`H264RateControlMode.PeakConstrainedVbr`), средний = `VideoBitrateBps`, пик ≈ 1.5×; **GOP 2 с** (`VideoKeyframeIntervalSeconds` → `CODECAPI_AVEncMPVGOPSize` + `MF_MT_MAX_KEYFRAME_SPACING`); CBR — `ConstantBitrate`. Спека: `RecordingVideoEncodingSpec`; тесты `Mp4SinkWriterConfigurationTests`, `Mp4SinkWriterTests` CBR/VBR.)_
- [x] Корректный `Finalize` при Stop и при ошибке (минимизировать битые файлы). _(`Mp4SinkWriter.Shutdown` Complete/AbortDueToError: finalize, удаление пустого/битого файла; `OutputPath`, `HasWrittenSamples`; тесты `Mp4SinkWriterFinalizeTests`.)_
- [ ] **Готово:** MP4 открывается штатными средствами; синхрон «на слух» приемлемый; таблица: Intel / NVIDIA / AMD (аппаратный MFT vs fallback).

---

## Фаза E — сквозная интеграция (неделя 5–6)

- [ ] `RecordingSession.Start(options)` с единой отменой.
- [ ] State machine: Idle → Arming → Recording → Stopping → Idle.
- [ ] Горячие клавиши (с учётом ограничений ОС) или честное «только в фокусе» в MVP.
- [ ] Индикатор записи (трей / оверлей опционально).
- [ ] Папка сохранения, шаблон имени, проверка свободного места до старта.
- [ ] Пауза: либо не в v1, либо отдельная сложная фича — явно решить.
- [ ] **Готово:** 30 мин записи без ручного вмешательства на тестовой машине.

---

## Фаза F — качество и стабильность (неделя 6–7)

- [ ] Стресс: смена разрешения во время записи; отключение микрофона; смена default audio; сон/гибернация (зафиксировать ожидаемое поведение).
- [ ] Политика дропов кадров + метрика в UI («не успевает кодирование»).
- [ ] Приватность UX: явная индикация записи; не поощнять скрытую запись.
- [ ] Release-логи минимальны; debug — трассировки этапов MF.
- [ ] Аудит зависимостей по расписанию.

---

## Фаза G — упаковка и распространение (неделя 7–8)

- [ ] Установка: MSIX **или** WiX/Inno — один выбранный путь.
- [ ] Подпись кода (снижение проблем SmartScreen).
- [ ] Semver + changelog.
- [ ] Документация: как записать; нет звука; где файлы; DRM/чёрный экран.

---

## Дополнения после ревью плана (обязательно учесть)

- [ ] **Ноутбук, гибридная графика:** на каком адаптере создавать D3D11 device для interop (избежать лагов/лишних копий).
- [ ] **Энергосбережение:** режимы 30/60 fps, битрейт; поведение на батарее; сообщение при троттлинге.
- [ ] **Честность про краши:** при kill процесса гарантий целостности MP4 нет; best-effort finalize только при контролируемом Stop/исключении (при необходимости — промежуточный контейнер/rename).
- [ ] **Тест-матрица железа:** минимум 3 конфигурации GPU; матрица аудио (встроенная, Bluetooth, внешний DAC).

---

## Критерии «план выполнен» для v1

- [ ] Стабильная запись экрана + loopback + mic в один MP4 (**v1:** одна смешанная AAC-LC дорожка; две дорожки — v1.1).
- [ ] Нет FFmpeg в релизной поставке.
- [ ] Документированы ограничения: DRM, смена аудио, длинные записи и дрейф, гибридная графика.
- [ ] Установщик + подпись (если цель — распространение).

---

_Файл создан как единый to-do по обсуждённому плану и его доработкам. Редактируй секции под свой MVP (один монитор vs область; для звука в v1 зафиксирован **микс** в одну AAC-LC дорожку — см. `RecordingAudioSpec`, отдельные дорожки — v1.1)._
