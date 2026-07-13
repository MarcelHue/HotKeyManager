using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Velopack;

namespace HotKeyManager.ViewModels;

/// <summary>
/// ViewModel fuer die Shell (MainWindow): Hook-/Treiber-Status im PaneFooter
/// und die Update-Benachrichtigung.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private static readonly Windows.UI.Color StatusGreen = Windows.UI.Color.FromArgb(255, 74, 222, 128);
    private static readonly Windows.UI.Color StatusAmber = Windows.UI.Color.FromArgb(255, 251, 191, 36);
    private static readonly Windows.UI.Color StatusGray = Windows.UI.Color.FromArgb(255, 107, 114, 128);

    private readonly UpdateService _updateService;
    private DispatcherQueue? _dispatcherQueue;
    private DispatcherQueueTimer? _updateCheckTimer;
    private UpdateInfo? _pendingUpdate;

    private static readonly Windows.UI.Color StatusRed = Windows.UI.Color.FromArgb(255, 239, 68, 68);

    [ObservableProperty]
    private string _hookStatusText = "Inaktiv";

    [ObservableProperty]
    private SolidColorBrush _hookStatusBrush = new(StatusRed);

    [ObservableProperty]
    private string _driverStatusText = "Nicht installiert";

    [ObservableProperty]
    private SolidColorBrush _driverStatusBrush = new(StatusGray);

    [ObservableProperty]
    private bool _isUpdateBannerVisible;

    [ObservableProperty]
    private string _updateBannerText = string.Empty;

    [ObservableProperty]
    private string _updateButtonText = "Jetzt aktualisieren";

    [ObservableProperty]
    private bool _isUpdateButtonEnabled = true;

    public string VersionText => $"Version {_updateService.CurrentVersion}";

    public MainViewModel(UpdateService? updateService = null)
    {
        _updateService = updateService ?? App.Current.UpdateService;
        RefreshStatus();
    }

    /// <summary>
    /// Aktualisiert Hook- und Treiber-Status (wird per Timer alle 5s aufgerufen).
    /// WICHTIG: Wenn der InterceptionService laeuft, den Status direkt ablesen.
    /// InputInterceptor.Initialize() (IsDriverActive) darf NICHT aufgerufen werden,
    /// waehrend der KeyboardHook aktiv ist — das erzeugt einen zweiten
    /// Interception-Context und fuehrt zu einem nativen Access Violation.
    /// </summary>
    public void RefreshStatus()
    {
        var hookRunning = App.Current.KeyboardHookService.IsRunning;
        HookStatusText = hookRunning ? "Aktiv" : "Inaktiv";
        HookStatusBrush = new SolidColorBrush(hookRunning ? StatusGreen : StatusRed);

        if (App.Current.InterceptionService.IsRunning)
        {
            DriverStatusText = "Aktiv";
            DriverStatusBrush = new SolidColorBrush(StatusGreen);
        }
        else if (InterceptionService.IsDriverInstalled())
        {
            DriverStatusText = "Neustart nötig";
            DriverStatusBrush = new SolidColorBrush(StatusAmber);
        }
        else
        {
            DriverStatusText = "Nicht installiert";
            DriverStatusBrush = new SolidColorBrush(StatusGray);
        }
    }

    /// <summary>
    /// Startet Update-Checks: einmal sofort, danach alle 6 Stunden.
    /// Vom MainWindow auf dem UI-Thread aufgerufen; No-Op im Entwicklungs-Build.
    /// </summary>
    public void StartUpdateChecks()
    {
        if (!_updateService.IsSupported) return;

        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _ = CheckForUpdatesAsync();

        _updateCheckTimer = _dispatcherQueue.CreateTimer();
        _updateCheckTimer.Interval = TimeSpan.FromHours(6);
        _updateCheckTimer.Tick += (s, e) => _ = CheckForUpdatesAsync();
        _updateCheckTimer.Start();
    }

    private async Task CheckForUpdatesAsync()
    {
        var updateInfo = await _updateService.CheckForUpdatesAsync();
        if (updateInfo != null)
            ShowUpdateAvailable(updateInfo);
    }

    /// <summary>Zeigt die Update-Leiste an (auch vom manuellen Check in den Einstellungen genutzt).</summary>
    public void ShowUpdateAvailable(UpdateInfo updateInfo)
    {
        _pendingUpdate = updateInfo;
        UpdateBannerText = $"Version {updateInfo.TargetFullRelease.Version} ist verfügbar.";
        IsUpdateBannerVisible = true;
    }

    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        if (_pendingUpdate == null) return;

        IsUpdateButtonEnabled = false;
        try
        {
            await _updateService.DownloadAndApplyAsync(_pendingUpdate, percent =>
            {
                // Progress-Callback kann von einem Hintergrund-Thread kommen
                _dispatcherQueue?.TryEnqueue(() => UpdateButtonText = $"Lade… {percent}%");
            });
            // App wird von Velopack neu gestartet — hierhin kommen wir normalerweise nicht mehr
        }
        catch (Exception ex)
        {
            UpdateBannerText = $"Update fehlgeschlagen: {ex.Message}";
            UpdateButtonText = "Erneut versuchen";
            IsUpdateButtonEnabled = true;
        }
    }
}
