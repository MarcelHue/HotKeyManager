using HotKeyManager.Services;
using InputInterceptorNS;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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

    public InterceptionService InterceptionService { get; } = new();

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
        this.InterceptionService.Stop();
        this.InterceptionService.Dispose();
        this.MidiService.Dispose();
        this.PadDeckBridge.Dispose();
        this.LogService.Dispose();
        this.trayIconService?.Dispose();
        this.MainWindow?.Close();
        Environment.Exit(0);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Elevated self-start for driver (un)installation: do the job and exit without UI.
        var commandLine = Environment.GetCommandLineArgs();
        if (commandLine.Contains("--install-driver"))
        {
            this.RunDriverCommand(InputInterceptor.InstallDriver, "Treiber-Installation via CLI fehlgeschlagen");
            return;
        }

        if (commandLine.Contains("--uninstall-driver"))
        {
            this.RunDriverCommand(InputInterceptor.UninstallDriver, "Treiber-Deinstallation via CLI fehlgeschlagen");
            return;
        }

        this.ActionExecutor = new ActionExecutor(this.InterceptionService);
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

    private void RunDriverCommand(Func<bool> command, string errorMessage)
    {
        try
        {
            command();
        }
        catch (Exception ex)
        {
            this.LogService.Error(errorMessage, ex);
        }

        Environment.Exit(0);
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

            // Hook and interception first: even if a UI handler throws during LoadHotkeys,
            // hotkeys and the driver keep working.
            this.KeyboardHookService.Start();
            if (InterceptionService.IsDriverActive())
            {
                this.InterceptionService.Start();
            }

            this.MidiService.Start();
            this.HotkeyManagerService.LoadHotkeys(configuration.Configuration.Hotkeys);

            // Connects quietly in the background whenever PadDeck runs.
            this.PadDeckBridge.Start();

            (this.MainWindow as MainWindow)?.UpdateStatusDisplay();
            await this.CheckAndPromptDriverInstallAsync();
        }
        catch (Exception ex)
        {
            this.LogService.Fatal("Kritischer Fehler beim Laden der Konfiguration", ex);
        }
    }

    private async Task CheckAndPromptDriverInstallAsync()
    {
        if (InterceptionService.IsDriverInstalled())
        {
            // Check IsRunning first: IsDriverActive (InputInterceptor.Initialize) must not be
            // called while the interception hook is already running.
            if (!this.InterceptionService.IsRunning && !InterceptionService.IsDriverActive())
            {
                await this.ShowDialogAsync(
                    "Neustart erforderlich",
                    "Der Interception-Treiber ist installiert, aber noch nicht aktiv. " +
                    "Bitte starte den Computer neu, damit der Treiber geladen wird.");
            }

            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = this.MainWindow?.Content.XamlRoot,
            Title = "Kernel-Mode Treiber nicht installiert",
            Content = "Der Interception-Treiber ist nicht installiert. Dieser Treiber wird fuer die Kernel-Mode "
                + "Tastatur-Injektion benoetigt (Tasten ohne LLKHF_INJECTED-Flag senden).\n\n"
                + "Moechtest du den Treiber jetzt installieren? (Erfordert Administrator-Rechte)",
            PrimaryButtonText = "Installieren",
            CloseButtonText = "Spaeter",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var (success, message) = await InterceptionService.InstallDriverAsync();
        if (success && InterceptionService.IsDriverActive())
        {
            this.InterceptionService.Start();
        }

        await this.ShowDialogAsync(success ? "Installation erfolgreich" : "Installation fehlgeschlagen", message);
        (this.MainWindow as MainWindow)?.UpdateStatusDisplay();
    }

    private async Task ShowDialogAsync(string title, string message)
    {
        if (this.MainWindow?.Content.XamlRoot is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = this.MainWindow.Content.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }
}
