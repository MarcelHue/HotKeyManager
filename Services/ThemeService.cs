using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.UI;

namespace HotKeyManager.Services;

/// <summary>
/// Applies the theme chosen in the settings (light, dark, system) to the main window.
/// Sets ElementTheme on the window content instead of Application.RequestedTheme, because that
/// can be switched at runtime and ContentDialogs inherit it.
/// </summary>
public static class ThemeService
{
    private static readonly Color DarkInactiveForeground = Color.FromArgb(255, 160, 160, 160);
    private static readonly Color DarkHoverBackground = Color.FromArgb(25, 255, 255, 255);
    private static readonly Color LightInactiveForeground = Color.FromArgb(255, 96, 96, 96);
    private static readonly Color LightHoverBackground = Color.FromArgb(25, 0, 0, 0);

    /// <summary>Applies a theme name from the settings ("light", "dark", anything else = system).</summary>
    public static void Apply(string? theme)
    {
        var window = App.Current.MainWindow;
        if (window?.Content is not FrameworkElement root)
        {
            return;
        }

        root.RequestedTheme = Parse(theme);
        UpdateTitleBarButtons(window, root.ActualTheme);
    }

    /// <summary>Colors the caption buttons (minimize, maximize, close) for the effective theme.</summary>
    public static void UpdateTitleBarButtons(Window window, ElementTheme actualTheme)
    {
        if (!AppWindowTitleBar.IsCustomizationSupported())
        {
            return;
        }

        var titleBar = window.AppWindow?.TitleBar;
        if (titleBar is null)
        {
            return;
        }

        titleBar.ButtonBackgroundColor = Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        if (actualTheme == ElementTheme.Dark)
        {
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveForegroundColor = DarkInactiveForeground;
            titleBar.ButtonHoverBackgroundColor = DarkHoverBackground;
            titleBar.ButtonHoverForegroundColor = Colors.White;
        }
        else
        {
            titleBar.ButtonForegroundColor = Colors.Black;
            titleBar.ButtonInactiveForegroundColor = LightInactiveForeground;
            titleBar.ButtonHoverBackgroundColor = LightHoverBackground;
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
