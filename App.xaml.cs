using Microsoft.UI.Xaml;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;
using InputInterceptorNS;

namespace HotKeyManager;

public partial class App : Application
{
    private Window? _mainWindow;
    private TrayIconService? _trayIconService;

    public static new App Current => (App)Application.Current;
    public Window? MainWindow => _mainWindow;
    public DispatcherQueue DispatcherQueue => _mainWindow?.DispatcherQueue!;

    // Services
    public LogService LogService { get; } = new();
    public ConfigurationService ConfigurationService { get; } = new();
    public KeyboardHookService KeyboardHookService { get; } = new();
    public InterceptionService InterceptionService { get; } = new();
    public HotkeyManagerService HotkeyManagerService { get; private set; } = null!;
    public ActionExecutor ActionExecutor { get; private set; } = null!;
    public AutoStartService AutoStartService { get; } = new();

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Kommandozeilen-Argumente pruefen fuer Treiber-Installation als Admin
        var cmdArgs = Environment.GetCommandLineArgs();
        if (cmdArgs.Contains("--install-driver"))
        {
            try
            {
                InputInterceptor.InstallDriver();
            }
            catch (Exception ex)
            {
                LogService.Error("Treiber-Installation via CLI fehlgeschlagen", ex);
            }
            Environment.Exit(0);
            return;
        }
        if (cmdArgs.Contains("--uninstall-driver"))
        {
            try
            {
                InputInterceptor.UninstallDriver();
            }
            catch (Exception ex)
            {
                LogService.Error("Treiber-Deinstallation via CLI fehlgeschlagen", ex);
            }
            Environment.Exit(0);
            return;
        }
        
        // Initialize services
        ActionExecutor = new ActionExecutor(InterceptionService);
        HotkeyManagerService = new HotkeyManagerService(KeyboardHookService, ActionExecutor, ConfigurationService);

        _mainWindow = new MainWindow();
        _mainWindow.Activate();

        // Initialize tray icon after window is created
        _trayIconService = new TrayIconService(_mainWindow);

        // Load configuration
        _ = LoadConfigurationAsync();
    }

    private async Task LoadConfigurationAsync()
    {
        await ConfigurationService.LoadAsync();
        LogService.MinLogLevel = LogService.ParseLogLevel(
            ConfigurationService.Configuration.Settings.LogLevel);
        HotkeyManagerService.LoadHotkeys(ConfigurationService.Configuration.Hotkeys);
        KeyboardHookService.Start();
        
        // Interception-Service starten, falls Treiber aktiv ist
        if (InterceptionService.IsDriverActive())
        {
            InterceptionService.Start();
        }
        
        // Statusanzeige im MainWindow aktualisieren
        (_mainWindow as MainWindow)?.UpdateStatusDisplay();
        
        // Automatische Treiber-Erkennung
        await CheckAndPromptDriverInstallAsync();
    }

    private async Task CheckAndPromptDriverInstallAsync()
    {
        if (InterceptionService.IsDriverInstalled())
        {
            if (!InterceptionService.IsDriverActive())
            {
                await ShowDialogAsync(
                    "Neustart erforderlich",
                    "Der Interception-Treiber ist installiert, aber noch nicht aktiv. " +
                    "Bitte starte den Computer neu, damit der Treiber geladen wird.");
            }
            return;
        }

        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            XamlRoot = _mainWindow?.Content.XamlRoot,
            Title = "Kernel-Mode Treiber nicht installiert",
            Content = "Der Interception-Treiber ist nicht installiert. Dieser Treiber wird fuer " +
                      "die Kernel-Mode Tastatur-Injektion benoetigt (Tasten ohne LLKHF_INJECTED-Flag senden).\n\n" +
                      "Moechtest du den Treiber jetzt installieren? (Erfordert Administrator-Rechte)",
            PrimaryButtonText = "Installieren",
            CloseButtonText = "Spaeter",
            DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        if (result == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
        {
            var (success, message) = await InterceptionService.InstallDriverAsync();
            
            if (success && InterceptionService.IsDriverActive())
            {
                InterceptionService.Start();
            }
            
            await ShowDialogAsync(success ? "Installation erfolgreich" : "Installation fehlgeschlagen", message);
            (_mainWindow as MainWindow)?.UpdateStatusDisplay();
        }
    }

    private async Task ShowDialogAsync(string title, string message)
    {
        if (_mainWindow?.Content.XamlRoot == null) return;
        var dialog = new Microsoft.UI.Xaml.Controls.ContentDialog
        {
            XamlRoot = _mainWindow.Content.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    public void ShowMainWindow()
    {
        if (_mainWindow != null)
        {
            _mainWindow.Activate();
        }
    }

    public void ExitApplication()
    {
        KeyboardHookService.Stop();
        InterceptionService.Stop();
        InterceptionService.Dispose();
        LogService.Dispose();
        _trayIconService?.Dispose();
        _mainWindow?.Close();
        Environment.Exit(0);
    }
}
