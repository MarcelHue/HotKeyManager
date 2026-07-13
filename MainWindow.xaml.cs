using HotKeyManager.Helpers;
using HotKeyManager.Services;
using HotKeyManager.ViewModels;
using HotKeyManager.Views;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace HotKeyManager;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();

    public MainWindow()
    {
        this.InitializeComponent();
        SetupWindow();

        ContentFrame.Navigate(typeof(HotkeyListPage));
        NavView.SelectedItem = HotkeysNavItem;

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

    public void UpdateDriverStatus() => ViewModel.RefreshDriverStatus();

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
