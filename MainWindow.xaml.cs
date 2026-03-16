using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Microsoft.UI;
using HotKeyManager.Helpers;
using HotKeyManager.Services;
using HotKeyManager.Views;
using WinRT.Interop;

namespace HotKeyManager;

public sealed partial class MainWindow : Window
{
    private AppWindow? _appWindow;
    private DispatcherTimer? _statusTimer;
    private DispatcherTimer? _infoDismissTimer;

    private static readonly SolidColorBrush GreenBrush = new(Windows.UI.Color.FromArgb(255, 74, 222, 128));
    private static readonly SolidColorBrush YellowBrush = new(Windows.UI.Color.FromArgb(255, 250, 204, 21));
    private static readonly SolidColorBrush RedBrush = new(Windows.UI.Color.FromArgb(255, 239, 68, 68));

    public AppWindow? AppWindow => _appWindow;

    public Windows.Graphics.SizeInt32 CurrentSize => _appWindow?.Size ?? new Windows.Graphics.SizeInt32(GlobalConst.STARTUP_WINDOW_WIDTH, GlobalConst.STARTUP_WINDOW_HEIGHT);

    public MainWindow()
    {
        this.InitializeComponent();

        SetupWindow();
        ContentFrame.Navigate(typeof(HotkeyListPage));
        UpdateNavSelection("Hotkeys");
        
        StartStatusTimer();
        SubscribeToLogErrors();
    }

    private void SetupWindow()
    {
        // Get AppWindow
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            // Set window size
            _appWindow.Resize(new Windows.Graphics.SizeInt32(GlobalConst.STARTUP_WINDOW_WIDTH, GlobalConst.STARTUP_WINDOW_HEIGHT));

            // Set title bar
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titleBar = _appWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(30, 255, 255, 255);
            }

            // Center window
            var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
            var centerX = (displayArea.WorkArea.Width - GlobalConst.STARTUP_WINDOW_WIDTH) / 2;
            var centerY = (displayArea.WorkArea.Height - GlobalConst.STARTUP_WINDOW_HEIGHT) / 2;
            _appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
        }

        // Handle close to minimize to tray
        this.Closed += MainWindow_Closed;
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var config = App.Current.ConfigurationService.Configuration;
        if (config.Settings.MinimizeToTray)
        {
            args.Handled = true;
            _appWindow?.Hide();
        }
    }

    public void ShowWindow()
    {
        _appWindow?.Show();
        this.Activate();
    }

    private void NavHotkeys_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HotkeyListPage));

        UpdateNavSelection("Hotkeys");
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(SettingsPage));
        UpdateNavSelection("Settings");
    }

    private void UpdateNavSelection(string selected)
    {
        NavHotkeysButton.Background = selected == "Hotkeys"
            ? (Brush)Application.Current.Resources["SurfaceBrush"]
            : new SolidColorBrush(Colors.Transparent);

        NavSettingsButton.Background = selected == "Settings"
            ? (Brush)Application.Current.Resources["SurfaceBrush"]
            : new SolidColorBrush(Colors.Transparent);
    }

    private void StartStatusTimer()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusTimer.Tick += (_, _) => UpdateStatusDisplay();
        _statusTimer.Start();
    }

    private void SubscribeToLogErrors()
    {
        App.Current.LogService.ErrorOccurred += OnLogError;
    }

    private void OnLogError(object? sender, Services.LogMessageEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ErrorInfoBar.Severity = e.Level == Services.LogLevel.Error
                ? Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error
                : Microsoft.UI.Xaml.Controls.InfoBarSeverity.Warning;
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

    public void UpdateStatusDisplay()
    {
        var hookRunning = App.Current.KeyboardHookService.IsRunning;
        HookStatusDot.Fill = hookRunning ? GreenBrush : RedBrush;
        HookStatusText.Text = hookRunning ? "Aktiv" : "Inaktiv";

        var driverInstalled = InterceptionService.IsDriverInstalled();
        var driverActive = driverInstalled && InterceptionService.IsDriverActive();

        if (driverActive)
        {
            DriverStatusDot.Fill = GreenBrush;
            DriverStatusText.Text = "Aktiv";
        }
        else if (driverInstalled)
        {
            DriverStatusDot.Fill = YellowBrush;
            DriverStatusText.Text = "Neustart noetig";
        }
        else
        {
            DriverStatusDot.Fill = RedBrush;
            DriverStatusText.Text = "Nicht installiert";
        }
    }
}
