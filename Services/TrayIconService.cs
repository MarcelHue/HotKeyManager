using System.Drawing;
using CommunityToolkit.Mvvm.Input;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Services;

/// <summary>Tray icon with an open/exit context menu; double-click shows the main window.</summary>
public class TrayIconService : IDisposable
{
    private const string IconFileName = "icon-512.ico";
    private const string FallbackIconText = "HK";

    private readonly TaskbarIcon trayIcon;
    private bool disposed;

    /// <summary>Creates the tray icon and shows it right away.</summary>
    public TrayIconService()
    {
        this.trayIcon = new TaskbarIcon
        {
            ToolTipText = "HotKey Manager",
            ContextFlyout = this.CreateContextMenu(),
            DoubleClickCommand = new RelayCommand(this.ShowMainWindow),
            NoLeftClickDelay = true
        };

        this.LoadIcon();
        this.trayIcon.ForceCreate();
    }

    /// <summary>Removes the tray icon.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.trayIcon.Dispose();
        this.disposed = true;
    }

    private static GeneratedIconSource CreateFallbackIcon() => new()
    {
        Text = FallbackIconText,
        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
    };

    private MenuFlyout CreateContextMenu()
    {
        var contextMenu = new MenuFlyout();
        contextMenu.Items.Add(new MenuFlyoutItem { Text = "Öffnen", Command = new RelayCommand(this.ShowMainWindow) });
        contextMenu.Items.Add(new MenuFlyoutSeparator());
        contextMenu.Items.Add(new MenuFlyoutItem
        {
            Text = "Beenden",
            Command = new RelayCommand(() => App.Current.ExitApplication())
        });
        return contextMenu;
    }

    private void LoadIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, IconFileName);
            if (File.Exists(iconPath))
            {
                this.trayIcon.Icon = new Icon(iconPath);
            }
            else
            {
                this.trayIcon.IconSource = CreateFallbackIcon();
            }
        }
        catch (Exception ex)
        {
            App.Current.LogService.Debug($"Tray-Icon konnte nicht geladen werden: {ex.Message}");
            this.trayIcon.IconSource = CreateFallbackIcon();
        }
    }

    private void ShowMainWindow() => App.Current.ShowMainWindow();
}
