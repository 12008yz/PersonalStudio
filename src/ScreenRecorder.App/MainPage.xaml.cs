using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using ScreenRecorder.RecordingEngine;
using ScreenRecorder.RecordingEngine.Audio;
using ScreenRecorder.RecordingEngine.Capture;
using ScreenRecorder.RecordingEngine.Settings;

// To learn more about WinUI, the WinUI project structure,
// and more about your project templates, see: http://aka.ms/winui-project-info.

namespace ScreenRecorder_App;

/// <summary>
/// The main content area of the application window.
/// </summary>
public sealed partial class MainPage : Page
{
    private IAppSettingsStore? _settingsStore;
    private AppSettings? _currentSettings;
    private bool _audioPickersBusy;

    public MainPage()
    {
        InitializeComponent();
    }

    private sealed class AudioPickerRow
    {
        public string? EndpointId { get; init; }

        public string DisplayLabel { get; init; } = "";
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        var activityLog = App.Services.GetRequiredService<ActivityLog>();
        var store = App.Services.GetRequiredService<IAppSettingsStore>();
        var logger = App.Services.GetRequiredService<ILogger<MainPage>>();
        var loader = new ResourceLoader();

        ActivityLogItems.ItemsSource = activityLog.Entries;
        _settingsStore = store;

        void AppendUi(string line)
        {
            _ = DispatcherQueue.TryEnqueue(() => activityLog.Entries.Add(line));
        }

        try
        {
            var settings = await store.LoadOrCreateAsync().ConfigureAwait(true);

            try
            {
                var micRows = BuildAudioRows(AudioDeviceEnumeration.EnumerateCaptureEndpoints(), loader);
                var renderRows = BuildAudioRows(AudioDeviceEnumeration.EnumerateRenderEndpoints(), loader);

                var micPref = NormalizeEndpointPreferenceAgainstRows(micRows, settings.PreferredMicrophoneEndpointId);
                var loopPref = NormalizeEndpointPreferenceAgainstRows(renderRows, settings.PreferredLoopbackRenderEndpointId);
                var reconciled = settings with
                {
                    PreferredMicrophoneEndpointId = micPref,
                    PreferredLoopbackRenderEndpointId = loopPref,
                };

                ApplyComboRows(MicrophoneCombo, micRows, micPref);
                ApplyComboRows(LoopbackCombo, renderRows, loopPref);

                _currentSettings = reconciled;

                if (reconciled != settings)
                {
                    await store.SaveAsync(reconciled).ConfigureAwait(true);
                    logger.LogInformation("Pruned stale or unknown audio endpoint id(s) in settings.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Audio device enumeration failed");
                _currentSettings = settings;
            }

            _currentSettings ??= settings;

            var path = ScreenRecorder.RecordingEngine.ApplicationIdentity.DefaultSettingsFilePath;
            logger.LogInformation("Settings ready at {Path}", path);

            AppendUi($"[{DateTime.Now:HH:mm:ss}] {path}");
            if (!string.IsNullOrEmpty(settings.LastOutputDirectory))
                AppendUi($"[{DateTime.Now:HH:mm:ss}] {settings.LastOutputDirectory}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Settings load failed");
            AppendUi($"[{DateTime.Now:HH:mm:ss}] {ex.Message}");
        }
    }

    private static string FormatScreenCaptureFailureForUser(
        ScreenCaptureFailureKind kind,
        Exception ex,
        ResourceLoader loader)
    {
        var key = kind switch
        {
            ScreenCaptureFailureKind.AccessDenied => "CaptureError_AccessDenied",
            ScreenCaptureFailureKind.ResourceBusy => "CaptureError_ResourceBusy",
            ScreenCaptureFailureKind.AccessLostOrDeviceFailed => "CaptureError_AccessLostOrDevice",
            ScreenCaptureFailureKind.InvalidArgument => "CaptureError_InvalidArgument",
            ScreenCaptureFailureKind.ObjectDisposedOrClosed => "CaptureError_ObjectDisposed",
            _ => null,
        };

        if (key is not null)
        {
            var s = loader.GetString(key);
            if (!string.IsNullOrWhiteSpace(s))
                return s;
        }

        var fallback = loader.GetString("CaptureError_UnknownWithDetail") ?? "{0}";
        return string.Format(CultureInfo.CurrentCulture, fallback, ex.Message);
    }

    private static List<AudioPickerRow> BuildAudioRows(
        IReadOnlyList<AudioEndpointDescriptor> endpoints,
        ResourceLoader loader)
    {
        var defaultLabel = loader.GetString("AudioPicker_SystemDefault") ?? "System default";
        var rows = new List<AudioPickerRow>(1 + endpoints.Count)
        {
            new() { EndpointId = null, DisplayLabel = defaultLabel },
        };

        foreach (var d in endpoints)
        {
            rows.Add(new AudioPickerRow
            {
                EndpointId = d.DeviceId,
                DisplayLabel = d.DisplayName,
            });
        }

        return rows;
    }

    /// <summary>
    /// Если сохранённый id больше не встречается среди активных устройств, возвращает null (как у строки «По умолчанию»),
    /// чтобы JSON и выбранная строка ComboBox не расходились.
    /// </summary>
    private static string? NormalizeEndpointPreferenceAgainstRows(IReadOnlyList<AudioPickerRow> rows, string? preferredId)
    {
        if (string.IsNullOrWhiteSpace(preferredId))
            return null;

        foreach (var r in rows)
        {
            if (r.EndpointId is not null &&
                string.Equals(r.EndpointId, preferredId, StringComparison.OrdinalIgnoreCase))
                return r.EndpointId;
        }

        return null;
    }

    private void ApplyComboRows(ComboBox combo, IReadOnlyList<AudioPickerRow> rows, string? preferredEndpointId)
    {
        if (rows.Count == 0)
            return;

        _audioPickersBusy = true;
        try
        {
            combo.ItemsSource = rows;

            var selected = preferredEndpointId is null
                ? rows[0]
                : rows.FirstOrDefault(r =>
                      r.EndpointId != null &&
                      string.Equals(r.EndpointId, preferredEndpointId, StringComparison.OrdinalIgnoreCase))
                  ?? rows[0];

            combo.SelectedItem = selected;
        }
        finally
        {
            _audioPickersBusy = false;
        }
    }

    private async void AudioDeviceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_audioPickersBusy || _settingsStore is null || _currentSettings is null)
            return;

        if (sender is not ComboBox cb)
            return;

        if (cb.SelectedItem is not AudioPickerRow row)
            return;

        var logger = App.Services.GetRequiredService<ILogger<MainPage>>();

        try
        {
            if (ReferenceEquals(cb, MicrophoneCombo))
            {
                var next = _currentSettings with { PreferredMicrophoneEndpointId = row.EndpointId };
                await _settingsStore.SaveAsync(next).ConfigureAwait(true);
                _currentSettings = next;
            }
            else if (ReferenceEquals(cb, LoopbackCombo))
            {
                var next = _currentSettings with { PreferredLoopbackRenderEndpointId = row.EndpointId };
                await _settingsStore.SaveAsync(next).ConfigureAwait(true);
                _currentSettings = next;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saving audio device preference failed");
        }
    }

    private async void CaptureTestButton_Click(object sender, RoutedEventArgs e)
    {
        await RunMonitorCaptureTestAsync(10, "CaptureTest_Started").ConfigureAwait(true);
    }

    private async void CaptureTest60Button_Click(object sender, RoutedEventArgs e)
    {
        await RunMonitorCaptureTestAsync(60, "CaptureTest60_Started").ConfigureAwait(true);
    }

    private async Task RunMonitorCaptureTestAsync(int durationSeconds, string startedKey)
    {
        var activityLog = App.Services.GetRequiredService<ActivityLog>();
        var logger = App.Services.GetRequiredService<ILogger<MainPage>>();
        var loader = new ResourceLoader();
        var culture = CultureInfo.CurrentCulture;

        void AppendUiLine(string line)
        {
            // Метод вызывается из UI-события и далее продолжает исполняться на UI-потоке:
            // добавляем в ObservableCollection напрямую, без фоновых enqueue.
            activityLog.Entries.Add($"[{DateTime.Now:HH:mm:ss}] {line}");
        }

        CaptureTestButton.IsEnabled = false;
        CaptureTest60Button.IsEnabled = false;

        try
        {
            var monitors = DisplayMonitorEnumeration.EnumerateMonitors();
            var target = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
            if (target is null)
            {
                AppendUiLine(loader.GetString("CaptureTest_NoMonitors") ?? string.Empty);
                return;
            }

            var startedFmt = loader.GetString(startedKey) ?? "{0}";
            AppendUiLine(string.Format(CultureInfo.CurrentCulture, startedFmt, target.DeviceName));

            using var session = new MonitorFrameCaptureSession(
                App.Services.GetService<ILogger<MonitorFrameCaptureSession>>());
            session.Start(target.MonitorHandle);

            var memFmt = loader.GetString("CaptureTest_MemorySample") ??
                         "Memory sample at {0:F0} s: WorkingSet {1:F1} MB, PrivateBytes {2:F1} MB.";

            var proc = Process.GetCurrentProcess();
            var memStopwatch = Stopwatch.StartNew();

            // RAM-сэмплинг делаем без фоновых Task.Run, чтобы не дергать UI-компоненты/COM-интерфейсы из другого потока.
            const int sampleEverySeconds = 5;
            while (true)
            {
                var remaining = durationSeconds - memStopwatch.Elapsed.TotalSeconds;
                if (remaining <= 0)
                    break;

                var delaySeconds = Math.Min(sampleEverySeconds, remaining);
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds)).ConfigureAwait(true);

                var elapsedSeconds = memStopwatch.Elapsed.TotalSeconds;
                var workingSetMb = proc.WorkingSet64 / 1024d / 1024d;
                var privateBytesMb = proc.PrivateMemorySize64 / 1024d / 1024d;

                AppendUiLine(string.Format(
                    culture,
                    memFmt,
                    elapsedSeconds,
                    workingSetMb,
                    privateBytesMb));
            }

            session.Stop();
            var m = session.GetMetrics();
            var resultFmt = loader.GetString("CaptureTest_Result") ?? string.Empty;
            var latencyMsFmt = loader.GetString("CaptureTest_LatencyMilliseconds") ?? "{0} ms";
            var latencyNa = loader.GetString("CaptureTest_LatencyNA") ?? string.Empty;

            static string FormatLatencyMs(IFormatProvider culture, string msTemplate, string na, double millis)
            {
                if (double.IsNaN(millis))
                    return na;

                var n = millis.ToString("F1", culture);
                return string.Format(culture, msTemplate, n);
            }

            AppendUiLine(string.Format(
                CultureInfo.CurrentCulture,
                resultFmt,
                m.FramesReceived,
                m.AverageFps,
                m.EmptyFrames,
                m.Elapsed.TotalSeconds,
                FormatLatencyMs(CultureInfo.CurrentCulture, latencyMsFmt, latencyNa, m.AverageFrameHandlerLatencyMilliseconds),
                FormatLatencyMs(CultureInfo.CurrentCulture, latencyMsFmt, latencyNa, m.LastFrameHandlerLatencyMilliseconds)));

            if (m.PoolRecreateFailureCount > 0)
            {
                var poolFmt = loader.GetString("CaptureTest_PoolRecreateFailures");
                if (!string.IsNullOrEmpty(poolFmt))
                {
                    AppendUiLine(string.Format(
                        CultureInfo.CurrentCulture,
                        poolFmt,
                        m.PoolRecreateFailureCount));
                }
            }
        }
        catch (Exception ex)
        {
            var kind = ScreenCaptureFailureClassifier.Classify(ex);
            logger.LogError(ex, "Capture test failed ({Kind})", kind);
            var errFmt = loader.GetString("CaptureTest_Error") ?? "{0}";
            var userMsg = FormatScreenCaptureFailureForUser(kind, ex, loader);
            AppendUiLine(string.Format(CultureInfo.CurrentCulture, errFmt, userMsg));
        }
        finally
        {
            CaptureTestButton.IsEnabled = true;
            CaptureTest60Button.IsEnabled = true;
        }
    }

    private async void DualAudioTestButton_Click(object sender, RoutedEventArgs e)
    {
        var activityLog = App.Services.GetRequiredService<ActivityLog>();
        var logger = App.Services.GetRequiredService<ILogger<MainPage>>();
        var loader = new ResourceLoader();

        void AppendUiLine(string line)
        {
            _ = DispatcherQueue.TryEnqueue(() => activityLog.Entries.Add($"[{DateTime.Now:HH:mm:ss}] {line}"));
        }

        DualAudioTestButton.IsEnabled = false;
        try
        {
            if (_currentSettings is null)
            {
                AppendUiLine(loader.GetString("DualAudioTest_SettingsNotReady") ?? string.Empty);
                return;
            }

            if (MicrophoneCombo.SelectedItem is not AudioPickerRow micPick ||
                LoopbackCombo.SelectedItem is not AudioPickerRow loopPick)
            {
                AppendUiLine(loader.GetString("DualAudioTest_SettingsNotReady") ?? string.Empty);
                return;
            }

            AppendUiLine(loader.GetString("DualAudioTest_Started") ?? string.Empty);

            var devicesFmt = loader.GetString("DualAudioTest_DevicesInUse");
            if (!string.IsNullOrEmpty(devicesFmt))
            {
                AppendUiLine(string.Format(
                    CultureInfo.CurrentCulture,
                    devicesFmt,
                    micPick.DisplayLabel,
                    loopPick.DisplayLabel));
            }

            long micBytes = 0;
            long loopBytes = 0;

            using var duo = new MicAndLoopbackCaptureSession(
                App.Services.GetService<ILogger<MicrophoneCaptureSession>>(),
                App.Services.GetService<ILogger<LoopbackCaptureSession>>());

            void OnMicPcm(object? _, PcmCaptureDataAvailableEventArgs e) =>
                Interlocked.Add(ref micBytes, e.PcmSamples.Length);

            void OnLoopPcm(object? _, PcmCaptureDataAvailableEventArgs e) =>
                Interlocked.Add(ref loopBytes, e.PcmSamples.Length);

            duo.Microphone.PcmDataAvailable += OnMicPcm;
            duo.Loopback.PcmDataAvailable += OnLoopPcm;

            try
            {
                try
                {
                    duo.Start(micPick.EndpointId, loopPick.EndpointId);
                }
                catch (InvalidOperationException ex)
                {
                    var errFmt = loader.GetString("DualAudioTest_Error") ?? "{0}";
                    AppendUiLine(string.Format(CultureInfo.CurrentCulture, errFmt, ex.Message));
                    return;
                }
                catch (ArgumentException ex)
                {
                    var errFmt = loader.GetString("DualAudioTest_Error") ?? "{0}";
                    AppendUiLine(string.Format(CultureInfo.CurrentCulture, errFmt, ex.Message));
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(true);

                duo.Stop();

                var resultFmt = loader.GetString("DualAudioTest_Result") ?? "{0} {1}";
                AppendUiLine(string.Format(
                    CultureInfo.CurrentCulture,
                    resultFmt,
                    Interlocked.Read(ref micBytes),
                    Interlocked.Read(ref loopBytes)));

                if (Interlocked.Read(ref loopBytes) == 0)
                {
                    var note = loader.GetString("DualAudioTest_LoopbackZeroNote");
                    if (!string.IsNullOrEmpty(note))
                        AppendUiLine(note);
                }

                if (Interlocked.Read(ref micBytes) == 0)
                {
                    AppendUiLine(
                        loader.GetString("DualAudioTest_MicZeroNote")
                        ?? "Microphone captured 0 bytes — speak during the test or check the microphone and privacy permissions.");
                }
            }
            finally
            {
                duo.Microphone.PcmDataAvailable -= OnMicPcm;
                duo.Loopback.PcmDataAvailable -= OnLoopPcm;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Dual-audio test failed");
            var errFmt = loader.GetString("DualAudioTest_Error") ?? "{0}";
            AppendUiLine(string.Format(CultureInfo.CurrentCulture, errFmt, ex.Message));
        }
        finally
        {
            DualAudioTestButton.IsEnabled = true;
        }
    }
}
