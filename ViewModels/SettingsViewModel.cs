using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Services;

namespace HotKeyManager.ViewModels;

/// <summary>View model of the settings page; every change is saved to the configuration right away.</summary>
public partial class SettingsViewModel : ObservableObject
{
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

    /// <summary>Creates the view model; services that are not passed in default to the app-wide instances.</summary>
    public SettingsViewModel(
        ConfigurationService? configService = null,
        AutoStartService? autoStartService = null)
    {
        this.configService = configService ?? App.Current.ConfigurationService;
        this.autoStartService = autoStartService ?? App.Current.AutoStartService;
    }

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
