using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Services;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.ViewModels;

/// <summary>View model of the settings page; every change is saved to the configuration right away.</summary>
public partial class SettingsViewModel : ObservableObject
{
    private const string InstallGlyph = "\uE9F5";
    private const string UninstallGlyph = "\uE74D";
    private const string UninstallText = "Deinstallieren";
    private const int DefaultThemeIndex = 2;
    private const int DefaultLogLevelIndex = 2;

    // Stored setting values; the order must match the radio buttons and combo box items in SettingsPage.
    private static readonly string[] ThemeNames = { "light", "dark", "system" };
    private static readonly string[] LogLevelNames = { "Debug", "Info", "Warning", "Error" };

    private readonly ConfigurationService configService;
    private readonly AutoStartService autoStartService;
    private bool isLoading;

    [ObservableProperty]
    private bool autoStart;

    [ObservableProperty]
    private bool minimizeToTray;

    [ObservableProperty]
    private bool startMinimized;

    /// <summary>0 = light, 1 = dark, 2 = system.</summary>
    [ObservableProperty]
    private int themeIndex;

    /// <summary>0 = Debug, 1 = Info, 2 = Warning, 3 = Error.</summary>
    [ObservableProperty]
    private int logLevelIndex = DefaultLogLevelIndex;

    [ObservableProperty]
    private InfoBarSeverity driverSeverity = InfoBarSeverity.Informational;

    [ObservableProperty]
    private string driverStatusMessage = "Wird geprüft...";

    [ObservableProperty]
    private string driverButtonText = "Installieren";

    [ObservableProperty]
    private string driverButtonGlyph = InstallGlyph;

    [ObservableProperty]
    private bool isDriverButtonEnabled = true;

    /// <summary>Creates the view model; services that are not passed in default to the app-wide instances.</summary>
    public SettingsViewModel(
        ConfigurationService? configService = null,
        AutoStartService? autoStartService = null)
    {
        this.configService = configService ?? App.Current.ConfigurationService;
        this.autoStartService = autoStartService ?? App.Current.AutoStartService;
    }

    /// <summary>True when the Interception driver is installed (it may still need a restart).</summary>
    public bool IsDriverInstalled => InterceptionService.IsDriverInstalled();

    /// <summary>Version line for the about card.</summary>
    public string VersionText => $"Version {App.Current.UpdateService.CurrentVersion}";

    /// <summary>Loads the values from the configuration without triggering the change handlers.</summary>
    public void Load()
    {
        this.isLoading = true;
        try
        {
            var settings = this.configService.Configuration.Settings;
            this.AutoStart = this.autoStartService.IsAutoStartEnabled;
            this.MinimizeToTray = settings.MinimizeToTray;
            this.StartMinimized = settings.StartMinimized;
            this.ThemeIndex = IndexOfName(ThemeNames, settings.Theme, DefaultThemeIndex);
            this.LogLevelIndex = IndexOfName(LogLevelNames, settings.LogLevel, DefaultLogLevelIndex);
        }
        finally
        {
            this.isLoading = false;
        }

        this.RefreshDriverStatus();
    }

    /// <summary>Determines the driver status and updates the driver card and the shell status.</summary>
    /// <remarks>
    /// While the InterceptionService runs, its state is read directly. IsDriverActive()
    /// (InputInterceptor.Initialize) must not be called again then: a second Interception context
    /// causes an access violation.
    /// </remarks>
    public void RefreshDriverStatus()
    {
        var installed = InterceptionService.IsDriverInstalled();
        var active = App.Current.InterceptionService.IsRunning
                     || (installed && InterceptionService.IsDriverActive());

        if (active)
        {
            this.DriverSeverity = InfoBarSeverity.Success;
            this.DriverStatusMessage = "Treiber ist aktiv und einsatzbereit.";
            this.DriverButtonText = UninstallText;
            this.DriverButtonGlyph = UninstallGlyph;
        }
        else if (installed)
        {
            this.DriverSeverity = InfoBarSeverity.Warning;
            this.DriverStatusMessage = "Treiber ist installiert, aber ein Neustart ist erforderlich.";
            this.DriverButtonText = UninstallText;
            this.DriverButtonGlyph = UninstallGlyph;
        }
        else
        {
            this.DriverSeverity = InfoBarSeverity.Informational;
            this.DriverStatusMessage =
                "Treiber ist nicht installiert. Für Kernel-Mode Injektion wird er benötigt.";
            this.DriverButtonText = "Installieren";
            this.DriverButtonGlyph = InstallGlyph;
        }

        if (App.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.ViewModel.RefreshStatus();
        }
    }

    /// <summary>Installs the Interception driver and starts it if Windows loads it without a restart.</summary>
    public async Task<(bool Success, string Message)> InstallDriverAsync()
    {
        this.IsDriverButtonEnabled = false;
        this.DriverButtonText = "Wird installiert...";

        var result = await InterceptionService.InstallDriverAsync();

        // Some systems load the driver without a restart.
        if (result.Success && InterceptionService.IsDriverActive())
        {
            App.Current.InterceptionService.Start();
        }

        this.IsDriverButtonEnabled = true;
        this.RefreshDriverStatus();
        return result;
    }

    /// <summary>Uninstalls the Interception driver.</summary>
    public async Task<(bool Success, string Message)> UninstallDriverAsync()
    {
        this.IsDriverButtonEnabled = false;
        this.DriverButtonText = "Wird deinstalliert...";

        var result = await InterceptionService.UninstallDriverAsync();

        this.IsDriverButtonEnabled = true;
        this.RefreshDriverStatus();
        return result;
    }

    private static int IndexOfName(string[] names, string? name, int defaultIndex)
    {
        var index = Array.FindIndex(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : defaultIndex;
    }

    private static string NameAtIndex(string[] names, int index, int defaultIndex)
    {
        var isInRange = (index >= 0) && (index < names.Length);
        return names[isInRange ? index : defaultIndex];
    }

    partial void OnAutoStartChanged(bool value)
    {
        if (this.isLoading)
        {
            return;
        }

        if (!this.autoStartService.SetAutoStart(value))
        {
            // The registry write failed: flip the toggle back without running this handler again.
            this.isLoading = true;
            this.AutoStart = !value;
            this.isLoading = false;
        }

        this.configService.Configuration.Settings.RunAtStartup = this.AutoStart;
        _ = this.configService.SaveAsync();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (this.isLoading)
        {
            return;
        }

        this.configService.Configuration.Settings.MinimizeToTray = value;
        _ = this.configService.SaveAsync();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        if (this.isLoading)
        {
            return;
        }

        this.configService.Configuration.Settings.StartMinimized = value;
        _ = this.configService.SaveAsync();
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (this.isLoading)
        {
            return;
        }

        var theme = NameAtIndex(ThemeNames, value, DefaultThemeIndex);
        this.configService.Configuration.Settings.Theme = theme;
        ThemeService.Apply(theme);
        _ = this.configService.SaveAsync();
    }

    partial void OnLogLevelIndexChanged(int value)
    {
        if (this.isLoading)
        {
            return;
        }

        var level = NameAtIndex(LogLevelNames, value, DefaultLogLevelIndex);
        this.configService.Configuration.Settings.LogLevel = level;
        App.Current.LogService.MinLogLevel = LogService.ParseLogLevel(level);
        _ = this.configService.SaveAsync();
    }
}
