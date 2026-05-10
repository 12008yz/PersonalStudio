using System.Globalization;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.ApplicationModel.Resources;
using ScreenRecorder.RecordingEngine;
using ScreenRecorder.RecordingEngine.Capture;
using ScreenRecorder.RecordingEngine.Settings;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ScreenRecorder_App;

/// <summary>
/// The main content page displayed inside the application window.
/// Add your UI logic, event handlers, and data binding here.
/// </summary>
public sealed partial class MainPage : Page
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        var activityLog = App.Services.GetRequiredService<ActivityLog>();
        var store = App.Services.GetRequiredService<IAppSettingsStore>();
        var logger = App.Services.GetRequiredService<ILogger<MainPage>>();

        ActivityLogItems.ItemsSource = activityLog.Entries;

        void AppendUi(string line)
        {
            _ = DispatcherQueue.TryEnqueue(() => activityLog.Entries.Add(line));
        }

        try
        {
            var settings = await store.LoadOrCreateAsync();
            var path = ApplicationIdentity.DefaultSettingsFilePath;
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

    private async void CaptureTestButton_Click(object sender, RoutedEventArgs e)
    {
        var activityLog = App.Services.GetRequiredService<ActivityLog>();
        var logger = App.Services.GetRequiredService<ILogger<MainPage>>();
        var loader = new ResourceLoader();

        void AppendUiLine(string line)
        {
            _ = DispatcherQueue.TryEnqueue(() => activityLog.Entries.Add($"[{DateTime.Now:HH:mm:ss}] {line}"));
        }

        CaptureTestButton.IsEnabled = false;
        try
        {
            var monitors = DisplayMonitorEnumeration.EnumerateMonitors();
            var target = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
            if (target is null)
            {
                AppendUiLine(loader.GetString("CaptureTest_NoMonitors") ?? string.Empty);
                return;
            }

            var startedFmt = loader.GetString("CaptureTest_Started") ?? "{0}";
            AppendUiLine(string.Format(CultureInfo.CurrentCulture, startedFmt, target.DeviceName));

            using var session = new MonitorFrameCaptureSession(
                App.Services.GetService<ILogger<MonitorFrameCaptureSession>>());
            session.Start(target.MonitorHandle);

            await Task.Delay(TimeSpan.FromSeconds(10));

            session.Stop();
            var m = session.GetMetrics();
            var resultFmt = loader.GetString("CaptureTest_Result") ?? string.Empty;
            AppendUiLine(string.Format(
                CultureInfo.CurrentCulture,
                resultFmt,
                m.FramesReceived,
                m.AverageFps,
                m.EmptyFrames,
                m.Elapsed.TotalSeconds));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Capture test failed");
            var errFmt = loader.GetString("CaptureTest_Error") ?? "{0}";
            AppendUiLine(string.Format(CultureInfo.CurrentCulture, errFmt, ex.Message));
        }
        finally
        {
            CaptureTestButton.IsEnabled = true;
        }
    }
}
