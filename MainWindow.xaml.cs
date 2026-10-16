using HotKeyManager.Helpers;
using HotKeyManager.Services;
using HotKeyManager.ViewModels;
using HotKeyManager.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Win32;

namespace HotKeyManager;

/// <summary>Main window with the navigation pane, status footer and error info bar.</summary>
public sealed partial class MainWindow : Window
{
    private static readonly TimeSpan StatusRefreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HookReinstallInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan InfoBarDismissDelay = TimeSpan.FromSeconds(5);

    private DispatcherTimer? statusTimer;
    private DispatcherTimer? hookReinstallTimer;
    private DispatcherTimer? infoDismissTimer;

    /// <summary>Creates the window, shows the hotkey list and starts the background timers.</summary>
    public MainWindow()
    {
        this.InitializeComponent();
        this.SetupWindow();

        this.ContentFrame.Navigate(typeof(HotkeyListPage));
        this.NavView.SelectedItem = this.HotkeysNavItem;

        this.StartStatusTimer();
        this.StartHookReinstallTimer();
        this.SubscribeToSystemEvents();
        this.SubscribeToLogErrors();

        this.ViewModel.StartUpdateChecks();
    }

    /// <summary>View model for the status footer and update banner.</summary>
    public MainViewModel ViewModel { get; } = new();

    /// <summary>Shows the window again after it was hidden to the tray.</summary>
    public void ShowWindow()
    {
        this.AppWindow?.Show();
        this.Activate();
    }

    /// <summary>Refreshes the hook and driver status in the footer.</summary>
    public void UpdateStatusDisplay() => this.ViewModel.RefreshStatus();

    private void SetupWindow()
    {
        this.SystemBackdrop = new MicaBackdrop();

        this.ExtendsContentIntoTitleBar = true;
        this.SetTitleBar(this.AppTitleBar);

        // The caption buttons do not follow the theme by themselves, also not on a system theme change.
        ThemeService.UpdateTitleBarButtons(this, this.RootGrid.ActualTheme);
        this.RootGrid.ActualThemeChanged +=
            (_, _) => ThemeService.UpdateTitleBarButtons(this, this.RootGrid.ActualTheme);

        if (this.AppWindow is not null)
        {
            this.CenterOnScreen(this.AppWindow);
        }

        this.Closed += this.MainWindow_Closed;
    }

    private void CenterOnScreen(AppWindow appWindow)
    {
        var size = new Windows.Graphics.SizeInt32(GlobalConst.StartupWindowWidth, GlobalConst.StartupWindowHeight);
        appWindow.Resize(size);

        var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
        var centerX = (displayArea.WorkArea.Width - GlobalConst.StartupWindowWidth) / 2;
        var centerY = (displayArea.WorkArea.Height - GlobalConst.StartupWindowHeight) / 2;
        appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var config = App.Current.ConfigurationService.Configuration;
        if (config.Settings.MinimizeToTray)
        {
            args.Handled = true;
            this.AppWindow?.Hide();
        }
    }

    private void StartStatusTimer()
    {
        this.statusTimer = new DispatcherTimer { Interval = StatusRefreshInterval };
        this.statusTimer.Tick += (_, _) => this.ViewModel.RefreshStatus();
        this.statusTimer.Start();
    }

    private void StartHookReinstallTimer()
    {
        // Windows silently removes low-level hooks (timeout, sleep/wake, lock);
        // reinstalling regularly keeps the hook alive.
        this.hookReinstallTimer = new DispatcherTimer { Interval = HookReinstallInterval };
        this.hookReinstallTimer.Tick += (_, _) => App.Current.KeyboardHookService.Reinstall();
        this.hookReinstallTimer.Start();
    }

    private void SubscribeToSystemEvents()
    {
        SystemEvents.SessionSwitch += this.OnSessionSwitch;
        SystemEvents.PowerModeChanged += this.OnPowerModeChanged;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            App.Current.LogService.Info("Session entsperrt - Hook wird reinstalliert");
            this.DispatcherQueue.TryEnqueue(() => App.Current.KeyboardHookService.Reinstall());
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            App.Current.LogService.Info("System aus Standby aufgewacht - Hook wird reinstalliert");
            this.DispatcherQueue.TryEnqueue(() => App.Current.KeyboardHookService.Reinstall());
        }
    }

    private void SubscribeToLogErrors() => App.Current.LogService.ErrorOccurred += this.OnLogError;

    // The logger raises this from any thread.
    private void OnLogError(object? sender, LogMessageEventArgs e) =>
        this.DispatcherQueue.TryEnqueue(() => this.ShowLogMessage(e));

    private void ShowLogMessage(LogMessageEventArgs e)
    {
        this.ErrorInfoBar.Severity = e.Level == LogLevel.Error ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
        this.ErrorInfoBar.Message = e.Message;
        this.ErrorInfoBar.IsOpen = true;

        this.infoDismissTimer?.Stop();
        this.infoDismissTimer = new DispatcherTimer { Interval = InfoBarDismissDelay };
        this.infoDismissTimer.Tick += (_, _) =>
        {
            this.ErrorInfoBar.IsOpen = false;
            this.infoDismissTimer.Stop();
        };
        this.infoDismissTimer.Start();
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            if (this.ContentFrame.CurrentSourcePageType != typeof(SettingsPage))
            {
                this.ContentFrame.Navigate(typeof(SettingsPage));
            }
        }
        else if (args.SelectedItem is NavigationViewItem { Tag: "hotkeys" })
        {
            if (this.ContentFrame.CurrentSourcePageType != typeof(HotkeyListPage))
            {
                this.ContentFrame.Navigate(typeof(HotkeyListPage));
            }
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (this.ContentFrame.CanGoBack)
        {
            this.ContentFrame.GoBack();
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        this.NavView.IsBackEnabled = this.ContentFrame.CanGoBack;

        // Keep the selection in sync with the actual page, e.g. after GoBack from the editor page.
        if (e.SourcePageType == typeof(SettingsPage))
        {
            this.NavView.SelectedItem = this.NavView.SettingsItem;
        }
        else if (e.SourcePageType == typeof(HotkeyListPage))
        {
            this.NavView.SelectedItem = this.HotkeysNavItem;
        }
    }
}
