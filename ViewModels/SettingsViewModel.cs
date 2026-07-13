using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Services;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ConfigurationService _configService;
    private readonly AutoStartService _autoStartService;
    private bool _isLoading;

    [ObservableProperty]
    private bool _autoStart;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _startMinimized;

    /// <summary>0 = Hell, 1 = Dunkel, 2 = System.</summary>
    [ObservableProperty]
    private int _themeIndex;

    [ObservableProperty]
    private InfoBarSeverity _driverSeverity = InfoBarSeverity.Informational;

    [ObservableProperty]
    private string _driverStatusMessage = "Wird geprüft...";

    [ObservableProperty]
    private string _driverButtonText = "Installieren";

    [ObservableProperty]
    private string _driverButtonGlyph = "";

    [ObservableProperty]
    private bool _isDriverButtonEnabled = true;

    public bool IsDriverInstalled => InterceptionService.IsDriverInstalled();

    public string VersionText => $"Version {App.Current.UpdateService.CurrentVersion}";

    public SettingsViewModel(
        ConfigurationService? configService = null,
        AutoStartService? autoStartService = null)
    {
        _configService = configService ?? App.Current.ConfigurationService;
        _autoStartService = autoStartService ?? App.Current.AutoStartService;
    }

    /// <summary>Laedt die Werte aus der Konfiguration, ohne die Change-Handler auszuloesen.</summary>
    public void Load()
    {
        _isLoading = true;
        try
        {
            var settings = _configService.Configuration.Settings;
            AutoStart = _autoStartService.IsAutoStartEnabled;
            MinimizeToTray = settings.MinimizeToTray;
            StartMinimized = settings.StartMinimized;
            ThemeIndex = settings.Theme?.ToLowerInvariant() switch
            {
                "light" => 0,
                "dark" => 1,
                _ => 2
            };
        }
        finally
        {
            _isLoading = false;
        }

        RefreshDriverStatus();
    }

    partial void OnAutoStartChanged(bool value)
    {
        if (_isLoading) return;

        if (!_autoStartService.SetAutoStart(value))
        {
            // Registry-Eintrag fehlgeschlagen -> Toggle zuruecksetzen
            _isLoading = true;
            AutoStart = !value;
            _isLoading = false;
        }

        _configService.Configuration.Settings.RunAtStartup = AutoStart;
        _ = _configService.SaveAsync();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        if (_isLoading) return;
        _configService.Configuration.Settings.MinimizeToTray = value;
        _ = _configService.SaveAsync();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        if (_isLoading) return;
        _configService.Configuration.Settings.StartMinimized = value;
        _ = _configService.SaveAsync();
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (_isLoading) return;

        var theme = value switch
        {
            0 => "light",
            1 => "dark",
            _ => "system"
        };
        _configService.Configuration.Settings.Theme = theme;
        ThemeService.Apply(theme);
        _ = _configService.SaveAsync();
    }

    public void RefreshDriverStatus()
    {
        if (InterceptionService.IsDriverActive())
        {
            DriverSeverity = InfoBarSeverity.Success;
            DriverStatusMessage = "Treiber ist aktiv und einsatzbereit.";
            DriverButtonText = "Deinstallieren";
            DriverButtonGlyph = "";
        }
        else if (InterceptionService.IsDriverInstalled())
        {
            DriverSeverity = InfoBarSeverity.Warning;
            DriverStatusMessage = "Treiber ist installiert, aber ein Neustart ist erforderlich.";
            DriverButtonText = "Deinstallieren";
            DriverButtonGlyph = "";
        }
        else
        {
            DriverSeverity = InfoBarSeverity.Informational;
            DriverStatusMessage = "Treiber ist nicht installiert. Für Kernel-Mode Injektion wird er benötigt.";
            DriverButtonText = "Installieren";
            DriverButtonGlyph = "";
        }

        // Shell-Statusanzeige mitziehen
        (App.Current.MainWindow as MainWindow)?.ViewModel.RefreshDriverStatus();
    }

    public async Task<(bool Success, string Message)> InstallDriverAsync()
    {
        IsDriverButtonEnabled = false;
        DriverButtonText = "Wird installiert...";

        var result = await InterceptionService.InstallDriverAsync();

        IsDriverButtonEnabled = true;
        RefreshDriverStatus();
        return result;
    }

    public async Task<(bool Success, string Message)> UninstallDriverAsync()
    {
        IsDriverButtonEnabled = false;
        DriverButtonText = "Wird deinstalliert...";

        var result = await InterceptionService.UninstallDriverAsync();

        IsDriverButtonEnabled = true;
        RefreshDriverStatus();
        return result;
    }
}
