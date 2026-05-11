# COM, апартаменты и потоки (RecordingEngine + WinUI)

Зафиксировано для реализации и ревью: **где STA/MTA**, **кто инициализирует COM**, **где допустим Media Foundation и будущий `IMFSinkWriter`**, **почему нельзя держать тяжёлый MF на UI-потоке**.

---

## 1. Кто вызывает `CoInitializeEx`

В управляемом коде проекта **нет явных вызовов** `CoInitializeEx` / `CoUninitialize`. Их при первом обращении к COM на потоке выполняет **CLR** (потоковая модель задаётся типом потока: STA или MTA).

Имеет смысл помнить:

- Поток с `[STAThread]` (точка входа классических приложений) → при первом COM-вызове обычно **STA**.
- Потоки пула (`ThreadPool`, продолжения `async` после `await` на пуле), `TaskScheduler.Default`, выделенные `Thread` без `SetApartmentState(STA)` в современном .NET → как правило **MTA**.

Проверка в отладке: при необходимости смотреть апартамент текущего потока (в современном .NET API на потоке помечено как устаревшее; у потоков пула в диагностике иногда встречается `Unknown` — это не отменяет правил выше для кода).

---

## 2. WinUI 3 (`ScreenRecorder.App`) — UI-поток = STA

Приложение WinUI 3 работает в модели **один UI-поток (STA)** с **`DispatcherQueue`**: обработка XAML, навигация, таймеры UI.

**Норма для UI-потока:**

- Команды пользователя, отображение состояния, короткие вызовы в движок.
- Маршалинг результатов обратно на UI через `DispatcherQueue` (или MVVM-паттерны поверх него).

**На UI-потоке не размещаем:**

- Длительные циклы кодирования, синхронное ожидание очереди энкодера, блокирующие вызовы MF (`IMFSinkWriter`, MFT, тяжёлый mux) — риск зависаний UI, таймаутов визуального дерева и взаимоблокировок с COM.

Сейчас тяжёлая работа **не на UI-потоке** за счёт колбэков захвата: **`FrameArrived`** (free-threaded пул WGC) и потоков WASAPI/NAudio для PCM. Очередь **`BoundedEncoderWorkQueue`** — готовая инфраструктура под энкодер (юнит-тесты в `RecordingEngine.Tests`), **в сквозной запись пока не встроена** (фаза D). При появлении **`IMFSinkWriter`** его жизненный цикл и вызовы MF должны быть **привязаны к выделенному MTA worker-потоку** (тот же поток, что обрабатывает очередь кодирования, либо строго согласованный с ним single-threaded consumer — см. §6).

---

## 3. Захват видео — `MonitorFrameCaptureSession`

- **Старт/стоп сессии** (`Start` / `Stop` с D3D-устройством и `GraphicsCaptureSession`) сегодня вызываются с потока, который держит вызывающий код (часто UI). Это **WinRT + Direct3D interop**; при смене вызывающего потока нужно сверяться с требованиями API и тестами.

- **`Direct3D11CaptureFramePool.CreateFreeThreaded`** — колбэк **`FrameArrived`** приходит с **произвольного потока** (не привязан к UI). Код в обработчике не должен напрямую трогать элементы XAML; метрики и логи — да, передача кадра в очередь энкодера — да (без долгой блокировки).

- **`OnItemClosed`** — избегает синхронного `Stop()` под замком; планирует остановку через **`ThreadPool.UnsafeQueueUserWorkItem`** (поток пула, MTA).

---

## 4. Захват аудио — WASAPI

### Микрофон — `MicrophoneCaptureSession`

Используется **`NAudio.Wave.WasapiCapture`**. Событие **`PcmDataAvailable`** приходит с **внутреннего потока захвата NAudio**, не с UI. Обработчики не должны долго блокироваться и не должны вызывать UI без маршалинга.

### Loopback — `LoopbackCaptureSession`

Старт **`WasapiCapture.StartRecording`** / **`ManualWasapiLoopbackCapture.StartRecording`** (внутри — `Initialize`/`Start` для loopback) на **чистом WinUI STA** часто даёт **`E_INVALIDARG`**. Поэтому оба пути вызывают старт через **`InvokeStartRecordingOnMtaWorker`** — отдельный поток с **`SetApartmentState(ApartmentState.MTA)`** в `LoopbackCaptureSession.cs`. Если `SetApartmentState(MTA)` недоступен (`PlatformNotSupportedException`), старт выполняется на **текущем** потоке вызывающего кода (редкий сценарий; на Windows десктопа обычно не срабатывает).

### Ручной loopback — `ManualWasapiLoopbackCapture`

Цепочка **`TryInitializeWithRetries`** / `AudioClient.Initialize` выполняется на **потоке вызывающего** `LoopbackCaptureSession.Start` (в тестах UI это часто STA); после успешного открытия **`StartRecording`** так же уходит в **`InvokeStartRecordingOnMtaWorker`**, как и для NAudio `WasapiCapture`. Отдельный **`Thread`** внутри класса читает буфер; апартамент по умолчанию в .NET для такого потока — **MTA** (явно STA не задаётся).

---

## 5. Media Foundation

### `MediaFoundationLifetime` (`MFStartup` / `MFShutdown`)

Счётчик ссылок на процесс: первый **`AddRef`** вызывает **`MediaFactory.MFStartup`**, последний **`Release`** — **`MFShutdown`**.

**Важно:** вызывать можно с разных потоков, но пары Add/Release должны быть сбалансированы; при появлении полноценного пайплайна разумно вызывать первый **`MFStartup`** с того потока, на котором будет жить энкодер (или раньше процесса один раз с MTA worker — политика на усмотрение реализации, но **не с UI** для долгих сессий).

### `MediaFoundationEncoderCatalog`

Перечисление MFT (`MFTEnumEx`) выполняется **на потоке вызывающего кода**. В репозитории сейчас это в основном **`MediaFoundationEncoderCatalogTests`** (MSTest); из **WinUI** пока не вызывается. Если добавите проверку кодеков с UI — вызывать **с фонового потока** или через `Task.Run` и не блокировать STA долгим синхронным вызовом.

### Будущий `IMFSinkWriter` (фаза D)

Рекомендуемая политика Microsoft для многопоточной подачи семплов — держать **один поток**, который владеет `IMFSinkWriter`, или строго сериализовать вызовы; на практике для стабильного mux удобно совместить с **`BoundedEncoderWorkQueue`**: весь MF-трафик записи — **только из worker-задачи** очереди (выделенный long-running поток, **MTA**).

---

## 6. `BoundedEncoderWorkQueue`

Worker создаётся через **`Task.Factory.StartNew(..., TaskCreationOptions.LongRunning, TaskScheduler.Default)`** — выделенный фоновый поток, для будущего MF-кода трактуем как **MTA consumer**. После интеграции в пайплайн продюсеры кадров/аудио смогут быть на **разных** потоках (видео `FrameArrived`, аудио WASAPI), а **очередь** даст одну сериализованную точку обработки и backpressure до энкодера.

---

## 7. Краткая таблица

| Область | Поток(и) | Заметки |
|--------|-----------|---------|
| WinUI XAML | STA (UI) | Не блокировать; не вызывать тяжёлый MF |
| `FrameArrived` (WGC free-threaded) | Пул / произвольный | Не трогать UI напрямую |
| WASAPI loopback start | Явный **MTA** worker | Обход `E_INVALIDARG` на WinUI STA |
| WASAPI capture / NAudio callbacks | MTA / фон | Маршалить на UI при необходимости |
| MF enumerate (сейчас — тесты) | Поток теста | Из UI — только через фон |
| Будущий SinkWriter + mux | **Не UI** | На worker `BoundedEncoderWorkQueue` (или экв. один MTA-поток) |

---

## 8. Ссылки

- [Overview of the Media Foundation threading model](https://learn.microsoft.com/windows/win32/medfound/media-foundation-threading-model) — базовые ожидания по MF и апартаментам.
- [AV_DRIFT_POLICY.md](AV_DRIFT_POLICY.md) — ось времени и метки семплов относительно worker энкодера.
- В репозитории: `src/ScreenRecorder.RecordingEngine/MediaFoundation/MediaFoundationLifetime.cs`, `BoundedEncoderWorkQueue.cs`, `Audio/LoopbackCaptureSession.cs`, `Capture/MonitorFrameCaptureSession.cs`.
