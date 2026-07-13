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

public sealed partial class MainWindow : Window
{
    private DispatcherTimer? _statusTimer;
    private DispatcherTimer? _hookReinstallTimer;
    private DispatcherTimer? _infoDismissTimer;

    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();
        SetupWindow();

        ContentFrame.Navigate(typeof(HotkeyListPage));
        NavView.SelectedItem = HotkeysNavItem;

        StartStatusTimer();
        StartHookReinstallTimer();
        SubscribeToSystemEvents();
        SubscribeToLogErrors();

        ViewModel.StartUpdateChecks();
    }

    private void SetupWindow()
    {
        SystemBackdrop = new MicaBackdrop();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // TitleBar-Button-Farben dem effektiven Theme nachfuehren (auch bei System-Theme-Wechsel)
        ThemeService.UpdateTitleBarButtons(this, RootGrid.ActualTheme);
        RootGrid.ActualThemeChanged += (s, e) => ThemeService.UpdateTitleBarButtons(this, RootGrid.ActualTheme);

        if (AppWindow != null)
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(GlobalConst.STARTUP_WINDOW_WIDTH, GlobalConst.STARTUP_WINDOW_HEIGHT));

            var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            var centerX = (displayArea.WorkArea.Width - GlobalConst.STARTUP_WINDOW_WIDTH) / 2;
            var centerY = (displayArea.WorkArea.Height - GlobalConst.STARTUP_WINDOW_HEIGHT) / 2;
            AppWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
        }

        this.Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var config = App.Current.ConfigurationService.Configuration;
        if (config.Settings.MinimizeToTray)
        {
            args.Handled = true;
            AppWindow?.Hide();
        }
    }

    public void ShowWindow()
    {
        AppWindow?.Show();
        this.Activate();
    }

    public void UpdateStatusDisplay() => ViewModel.RefreshStatus();

    private void StartStatusTimer()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => ViewModel.RefreshStatus();
        _statusTimer.Start();
    }

    private void StartHookReinstallTimer()
    {
        // Windows entfernt Low-Level-Hooks stillschweigend (Timeout, Sleep/Wake, Lock) —
        // regelmaessiges Reinstall haelt den Hook zuverlaessig am Leben.
        _hookReinstallTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _hookReinstallTimer.Tick += (_, _) => App.Current.KeyboardHookService.Reinstall();
        _hookReinstallTimer.Start();
    }

    private void SubscribeToSystemEvents()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            App.Current.LogService.Info("Session entsperrt - Hook wird reinstalliert");
            DispatcherQueue.TryEnqueue(() => App.Current.KeyboardHookService.Reinstall());
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            App.Current.LogService.Info("System aus Standby aufgewacht - Hook wird reinstalliert");
            DispatcherQueue.TryEnqueue(() => App.Current.KeyboardHookService.Reinstall());
        }
    }

    private void SubscribeToLogErrors()
    {
        App.Current.LogService.ErrorOccurred += OnLogError;
    }

    private void OnLogError(object? sender, LogMessageEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ErrorInfoBar.Severity = e.Level == LogLevel.Error
                ? InfoBarSeverity.Error
                : InfoBarSeverity.Warning;
            ErrorInfoBar.Message = e.Message;
            ErrorInfoBar.IsOpen = true;

            _infoDismissTimer?.Stop();
            _infoDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _infoDismissTimer.Tick += (_, _) =>
            {
                ErrorInfoBar.IsOpen = false;
                _infoDismissTimer.Stop();
            };
            _infoDismissTimer.Start();
        });
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.IsSettingsSelected)
        {
            if (ContentFrame.CurrentSourcePageType != typeof(SettingsPage))
                ContentFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.SelectedItem is NavigationViewItem { Tag: "hotkeys" })
        {
            if (ContentFrame.CurrentSourcePageType != typeof(HotkeyListPage))
                ContentFrame.Navigate(typeof(HotkeyListPage));
        }
    }

    private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        NavView.IsBackEnabled = ContentFrame.CanGoBack;

        // NavigationView-Auswahl mit der tatsaechlichen Seite synchron halten
        // (z.B. nach GoBack aus der Editor-Seite)
        if (e.SourcePageType == typeof(SettingsPage))
            NavView.SelectedItem = NavView.SettingsItem;
        else if (e.SourcePageType == typeof(HotkeyListPage))
            NavView.SelectedItem = HotkeysNavItem;
    }
}
