using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using ScreenRecorder.RecordingEngine;
using ScreenRecorder.RecordingEngine.Settings;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ScreenRecorder_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;
    private static bool _servicesConfigured;

    /// <summary>Корень DI для WinUI (фаза A).</summary>
    public static IServiceProvider Services { get; private set; } = default!;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (!_servicesConfigured)
            ConfigureServices();
        _window = new MainWindow();
        _window.Activate();
    }

    private static void ConfigureServices()
    {
        if (_servicesConfigured)
            return;

        var services = new ServiceCollection();
        services.AddSingleton<ActivityLog>();
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        services.AddSingleton<IAppSettingsStore>(sp =>
            new JsonAppSettingsStore(
                ApplicationIdentity.DefaultSettingsFilePath,
                sp.GetService<ILogger<JsonAppSettingsStore>>()));

        Services = services.BuildServiceProvider();
        _servicesConfigured = true;
    }
}
