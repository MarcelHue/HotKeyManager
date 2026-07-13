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
    public ConfigurationService ConfigurationService { get; } = new();
    public KeyboardHookService KeyboardHookService { get; } = new();
    public InterceptionService InterceptionService { get; } = new();
    public HotkeyManagerService HotkeyManagerService { get; private set; } = null!;
    public ActionExecutor ActionExecutor { get; private set; } = null!;
    public AutoStartService AutoStartService { get; } = new();
    public UpdateService UpdateService { get; } = new();

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
            catch { }
            Environment.Exit(0);
            return;
        }
        if (cmdArgs.Contains("--uninstall-driver"))
        {
            try
            {
                InputInterceptor.UninstallDriver();
            }
            catch { }
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
        ThemeService.Apply(ConfigurationService.Configuration.Settings.Theme);

        // Autostart-Registry-Eintrag auf den aktuellen Exe-Pfad auffrischen
        // (wichtig nach einem Update oder Umzug der Installation)
        if (ConfigurationService.Configuration.Settings.RunAtStartup)
            AutoStartService.SetAutoStart(true);

        HotkeyManagerService.LoadHotkeys(ConfigurationService.Configuration.Hotkeys);
        KeyboardHookService.Start();
        
        // Interception-Service starten, falls Treiber aktiv ist
        if (InterceptionService.IsDriverActive())
        {
            InterceptionService.Start();
        }
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
        _trayIconService?.Dispose();
        _mainWindow?.Close();
        Environment.Exit(0);
    }
}
