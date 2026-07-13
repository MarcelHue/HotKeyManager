using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace HotKeyManager.Services;

/// <summary>
/// Wendet das in den Einstellungen gewaehlte Theme (hell/dunkel/system) auf das Hauptfenster an.
/// Nutzt ElementTheme auf dem Fenster-Content statt Application.RequestedTheme,
/// damit zur Laufzeit umgeschaltet werden kann und ContentDialogs das Theme erben.
/// </summary>
public static class ThemeService
{
    public static void Apply(string? theme)
    {
        var window = App.Current.MainWindow;
        if (window?.Content is not FrameworkElement root) return;

        root.RequestedTheme = Parse(theme);
        UpdateTitleBarButtons(window, root.ActualTheme);
    }

    /// <summary>
    /// Passt die Farben der Fenster-Buttons (Minimieren/Maximieren/Schliessen) an das effektive Theme an.
    /// </summary>
    public static void UpdateTitleBarButtons(Window window, ElementTheme actualTheme)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported()) return;

        var titleBar = window.AppWindow?.TitleBar;
        if (titleBar == null) return;

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (actualTheme == ElementTheme.Dark)
        {
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 160, 160, 160);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 255, 255, 255);
            titleBar.ButtonHoverForegroundColor = Colors.White;
        }
        else
        {
            titleBar.ButtonForegroundColor = Colors.Black;
            titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 96, 96, 96);
            titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(25, 0, 0, 0);
            titleBar.ButtonHoverForegroundColor = Colors.Black;
        }
    }

    private static ElementTheme Parse(string? theme) => theme?.ToLowerInvariant() switch
    {
        "light" => ElementTheme.Light,
        "dark" => ElementTheme.Dark,
        _ => ElementTheme.Default
    };
}
