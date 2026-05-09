using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ScreenRecorder.RecordingEngine;
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
}
