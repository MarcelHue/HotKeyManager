using HotKeyManager.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace HotKeyManager;

/// <summary>Application entry: owns the service singletons, the main window and the tray icon.</summary>
public partial class App : Application
{
    private TrayIconService? trayIconService;

    /// <summary>Hooks up the global exception logging.</summary>
    public App()
    {
        this.InitializeComponent();

        this.UnhandledException += this.OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += this.OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += this.OnUnobservedTaskException;
    }

    public static new App Current => (App)Application.Current;

    public Window? MainWindow { get; private set; }

    public DispatcherQueue DispatcherQueue => this.MainWindow?.DispatcherQueue!;

    public LogService LogService { get; } = new();

    public ConfigurationService ConfigurationService { get; } = new();

    public KeyboardHookService KeyboardHookService { get; } = new();

    public HotkeyManagerService HotkeyManagerService { get; private set; } = null!;

    public ActionExecutor ActionExecutor { get; private set; } = null!;

    public AutoStartService AutoStartService { get; } = new();

    public UpdateService UpdateService { get; } = new();

    public MidiService MidiService { get; } = new();

    /// <summary>Connection to PadDeck for DisplayPad triggers; idle when PadDeck is not running.</summary>
    public PadDeckBridgeService PadDeckBridge { get; } = new();

    /// <summary>Brings the main window to the front.</summary>
    public void ShowMainWindow() => this.MainWindow?.Activate();

    /// <summary>Stops all hooks and services and ends the process.</summary>
    public void ExitApplication()
    {
        this.KeyboardHookService.Stop();
        this.MidiService.Dispose();
        this.PadDeckBridge.Dispose();
        this.LogService.Dispose();
        this.trayIconService?.Dispose();
        this.MainWindow?.Close();
        Environment.Exit(0);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        this.ActionExecutor = new ActionExecutor();
        this.HotkeyManagerService = new HotkeyManagerService(
            this.KeyboardHookService,
            this.ActionExecutor,
            this.ConfigurationService,
            this.MidiService,
            this.PadDeckBridge);

        this.MainWindow = new MainWindow();
        this.MainWindow.Activate();

        // After the window: the tray menu brings it to the front.
        this.trayIconService = new TrayIconService();

        _ = this.LoadConfigurationAsync();
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        this.LogService.Fatal("Unbehandelte UI-Exception", e.Exception);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e) =>
        this.LogService.Fatal("Unbehandelte AppDomain-Exception", e.ExceptionObject as Exception);

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        this.LogService.Fatal("Unbeobachtete Task-Exception", e.Exception?.InnerException ?? e.Exception);
        e.SetObserved();
    }

    private async Task LoadConfigurationAsync()
    {
        try
        {
            var configuration = this.ConfigurationService;
            await configuration.LoadAsync();
            this.LogService.MinLogLevel = LogService.ParseLogLevel(configuration.Configuration.Settings.LogLevel);

            ThemeService.Apply(configuration.Configuration.Settings.Theme);

            // One-time migration of old single actions into block macros (since v1.3.0).
            if (LegacyActionMigration.WrapLegacyActions(configuration.Configuration.Hotkeys))
            {
                this.LogService.Info("Alt-Aktionen wurden in Baustein-Makros migriert.");
                await configuration.SaveAsync();
            }

            // Refresh the autostart entry to the current exe path (it moves with updates).
            if (configuration.Configuration.Settings.RunAtStartup)
            {
                this.AutoStartService.SetAutoStart(true);
            }

            // Hook first: even if a UI handler throws during LoadHotkeys, hotkeys keep working.
            this.KeyboardHookService.Start();
            this.MidiService.Start();
            this.HotkeyManagerService.LoadHotkeys(configuration.Configuration.Hotkeys);

            // Connects quietly in the background whenever PadDeck runs.
            this.PadDeckBridge.Start();

            (this.MainWindow as MainWindow)?.UpdateStatusDisplay();
        }
        catch (Exception ex)
        {
            this.LogService.Fatal("Kritischer Fehler beim Laden der Konfiguration", ex);
        }
    }
}
