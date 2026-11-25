using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Velopack;

namespace HotKeyManager.ViewModels;

/// <summary>
/// View model of the shell (MainWindow): hook status in the pane footer
/// and the update notification.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private static readonly Windows.UI.Color StatusGreen = Windows.UI.Color.FromArgb(255, 74, 222, 128);
    private static readonly Windows.UI.Color StatusRed = Windows.UI.Color.FromArgb(255, 239, 68, 68);

    // Periodic checks let a long-running app still show new releases in time.
    private static readonly TimeSpan UpdateCheckInterval = TimeSpan.FromMinutes(15);

    private readonly UpdateService updateService;
    private DispatcherQueue? dispatcherQueue;
    private DispatcherQueueTimer? updateCheckTimer;
    private UpdateInfo? pendingUpdate;

    [ObservableProperty]
    private string hookStatusText = "Inaktiv";

    [ObservableProperty]
    private SolidColorBrush hookStatusBrush = new(StatusRed);

    [ObservableProperty]
    private bool isUpdateBannerVisible;

    [ObservableProperty]
    private string updateBannerText = string.Empty;

    [ObservableProperty]
    private string updateButtonText = "Jetzt aktualisieren";

    [ObservableProperty]
    private bool isUpdateButtonEnabled = true;

    /// <summary>Creates the view model; the update service defaults to the app-wide instance.</summary>
    public MainViewModel(UpdateService? updateService = null)
    {
        this.updateService = updateService ?? App.Current.UpdateService;
        this.RefreshStatus();
    }

    /// <summary>Version line for the pane footer.</summary>
    public string VersionText => $"Version {this.updateService.CurrentVersion}";

    /// <summary>Refreshes the hook status; the main window calls this on a timer.</summary>
    public void RefreshStatus()
    {
        var hookRunning = App.Current.KeyboardHookService.IsRunning;
        this.HookStatusText = hookRunning ? "Aktiv" : "Inaktiv";
        this.HookStatusBrush = new SolidColorBrush(hookRunning ? StatusGreen : StatusRed);
    }

    /// <summary>
    /// Starts the update checks: once right away, then periodically.
    /// Must be called on the UI thread; does nothing in a development build.
    /// </summary>
    public void StartUpdateChecks()
    {
        if (!this.updateService.IsSupported)
        {
            return;
        }

        this.dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _ = this.CheckForUpdatesAsync();

        this.updateCheckTimer = this.dispatcherQueue.CreateTimer();
        this.updateCheckTimer.Interval = UpdateCheckInterval;
        this.updateCheckTimer.Tick += (s, e) => _ = this.CheckForUpdatesAsync();
        this.updateCheckTimer.Start();
    }

    /// <summary>Shows the update banner; also used by the manual check in the settings.</summary>
    public void ShowUpdateAvailable(UpdateInfo updateInfo)
    {
        this.pendingUpdate = updateInfo;
        this.UpdateBannerText = $"Version {updateInfo.TargetFullRelease.Version} ist verfügbar.";
        this.IsUpdateBannerVisible = true;
    }

    private async Task CheckForUpdatesAsync()
    {
        var updateInfo = await this.updateService.CheckForUpdatesAsync();
        if (updateInfo is not null)
        {
            this.ShowUpdateAvailable(updateInfo);
        }
    }

    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        if (this.pendingUpdate is null)
        {
            return;
        }

        this.IsUpdateButtonEnabled = false;
        try
        {
            // The progress callback can run on a background thread.
            await this.updateService.DownloadAndApplyAsync(
                this.pendingUpdate,
                percent => this.dispatcherQueue?.TryEnqueue(() => this.UpdateButtonText = $"Lade… {percent}%"));

            // Velopack restarts the app, so this point is normally never reached.
        }
        catch (Exception ex)
        {
            this.UpdateBannerText = $"Update fehlgeschlagen: {ex.Message}";
            this.UpdateButtonText = "Erneut versuchen";
            this.IsUpdateButtonEnabled = true;
        }
    }
}
