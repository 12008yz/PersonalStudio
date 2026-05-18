# Ручной чеклист: фаза D — MP4 (плеер, синхрон, матрица GPU)

Автотесты (`Mp4PhaseDValidationTests`, `Mp4SinkWriterTests`) проверяют mux, `ftyp` и наличие MFT. **Открытие в плеере ОС** и **синхрон «на слух»** — только вручную на целевой машине.

---

## 1. Сгенерировать эталонный клип (движок)

Из корня репозитория:

```powershell
$env:SCREENRECORDER_KEEP_PHASED_MP4 = "1"
dotnet test src\ScreenRecorder.RecordingEngine.Tests\ScreenRecorder.RecordingEngine.Tests.csproj --filter "FullyQualifiedName~Mp4PhaseDValidationTests.Synthetic_av_mp4"
```

С переменной `SCREENRECORDER_KEEP_PHASED_MP4=1` файл **не удаляется** после теста — путь в выводе TestContext. Без переменной автотест сам удаляет временный `.mp4` (для CI).

Альтернатива — спайки (экран + звук без `IMFSinkWriter`):

```powershell
dotnet run --project src\ScreenRecorder.MfSpike\ScreenRecorder.MfSpike.csproj -c Release
dotnet run --project src\ScreenRecorder.VariantBSpike\ScreenRecorder.VariantBSpike.csproj -c Release
```

---

## 2. Открыть штатными средствами Windows

1. Двойной щелчок по `.mp4` → **Фильмы и телепрограммы** / **Кино и ТВ** (или VLC, если установлен).
2. Убедиться: воспроизведение **без ошибки кодека**, видео не «чёрное» (для синтетики — сплошной цвет), звук слышен (синус 440 Hz для Phase D теста).
3. Перемотка вперёд/назад — файл не обрывается.

**Критерий «ок»:** плеер ОС открывает файл без сообщения «неподдерживаемый формат».

---

## 3. Синхрон «на слух» (синтетика и будущий захват)

Для клипа из `Mp4PhaseDValidationTests` (5 с, 30 fps, общие таймстемпы на кадр):

- гул 440 Hz должен звучать **ровно** на протяжении ролика, без щелчков в конце;
- нет заметного сдвига звука относительно «конца» ролика (для статичного кадра визуального сдвига нет — слушайте обрыв/задержку в последней секунде).

После сквозной записи экрана (фаза E) повторить с YouTube + голосом; ориентир НФТ: **±100 ms** (см. `RecordingNfrSpec`).

---

## 4. MFT / GPU — заполнить матрицу

1. Запустить тест отчёта энкодеров:

   ```powershell
   dotnet test src\ScreenRecorder.RecordingEngine.Tests\ScreenRecorder.RecordingEngine.Tests.csproj --filter "FullyQualifiedName~Encoder_report_lists"
   ```

2. Скопировать блок **H.264 / AAC** из вывода в [HARDWARE_CODEC_MATRIX.md](HARDWARE_CODEC_MATRIX.md) (колонка «примечания» или новая строка с датой).
3. Отметить **HW vs SW** (в выводе `HW` / `SW` перед именем MFT).
4. Для конфигураций **AMD** и **только Intel iGPU** — отдельная строка на реальном ПК (план).

---

## 5. Критерий «фаза D готова» на этой машине

- [ ] Автотест `Mp4PhaseDValidationTests` зелёный.
- [ ] MP4 открывается встроенным плеером Windows.
- [ ] Синхрон синтетического клипа на слух приемлемый.
- [ ] В `HARDWARE_CODEC_MATRIX.md` есть строка с MFT (HW/SW) для этого ПК.

---

## Связанный код

- `Mp4SinkWriter`, `Mp4ContainerValidation`, `MediaFoundationEncoderReport`
- `docs/HARDWARE_CODEC_MATRIX.md`
