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

    public AppWindow? AppWindow => _appWindow;

    public Windows.Graphics.SizeInt32 CurrentSize => _appWindow?.Size ?? new Windows.Graphics.SizeInt32(GlobalConst.STARTUP_WINDOW_WIDTH, GlobalConst.STARTUP_WINDOW_HEIGHT);

    public MainWindow()
    {
        this.InitializeComponent();

        // Set window size and appearance
        SetupWindow();
        // Navigate to Hotkeys page by default
        ContentFrame.Navigate(typeof(HotkeyListPage));
        UpdateNavSelection("Hotkeys");
        UpdateDriverStatus();
    }

    public void UpdateDriverStatus()
    {
        if (InterceptionService.IsDriverActive())
        {
            DriverStatusIndicator.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 74, 222, 128));
            DriverStatusText.Text = "Aktiv";
        }
        else if (InterceptionService.IsDriverInstalled())
        {
            DriverStatusIndicator.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 251, 191, 36));
            DriverStatusText.Text = "Neustart nötig";
        }
        else
        {
            DriverStatusIndicator.Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 107, 114, 128));
            DriverStatusText.Text = "Nicht installiert";
        }
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
        // Update visual state of navigation buttons
        NavHotkeysButton.Background = selected == "Hotkeys"
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SurfaceBrush"]
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent);

        NavSettingsButton.Background = selected == "Settings"
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SurfaceBrush"]
            : new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.Transparent);
    }
}
