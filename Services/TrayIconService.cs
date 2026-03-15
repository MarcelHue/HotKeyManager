using System.Drawing;
using H.NotifyIcon;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System.Windows.Input;

namespace HotKeyManager.Services;

public class TrayIconService : IDisposable
{
    private readonly TaskbarIcon _trayIcon;
    private readonly Window _mainWindow;
    private bool _disposed;
    
    public TrayIconService(Window mainWindow)
    {
        _mainWindow = mainWindow;
        
        // Create WinUI MenuFlyout for context menu
        var contextMenu = new MenuFlyout();
        
        var openMenuItem = new MenuFlyoutItem 
        { 
            Text = "Öffnen",
            Command = new RelayCommand(ShowMainWindow)
        };
        contextMenu.Items.Add(openMenuItem);
        
        contextMenu.Items.Add(new MenuFlyoutSeparator());
        
        var exitMenuItem = new MenuFlyoutItem 
        { 
            Text = "Beenden",
            Command = new RelayCommand(() => App.Current.ExitApplication())
        };
        contextMenu.Items.Add(exitMenuItem);
        
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "HotKey Manager",
            ContextFlyout = contextMenu,
            DoubleClickCommand = new RelayCommand(ShowMainWindow),
            NoLeftClickDelay = true
        };
        
        // Load icon from .ico file
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "icon-512.ico");
            if (File.Exists(iconPath))
            {
                _trayIcon.Icon = new Icon(iconPath);
            }
            else
            {
                // Fallback to generated icon
                _trayIcon.IconSource = new GeneratedIconSource
                {
                    Text = "HK",
                    Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                    Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
                };
            }
        }
        catch
        {
            // Fallback to generated icon
            _trayIcon.IconSource = new GeneratedIconSource
            {
                Text = "HK",
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue)
            };
        }
        
        _trayIcon.ForceCreate();
    }
    
    private void ShowMainWindow()
    {
        App.Current.ShowMainWindow();
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _trayIcon.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Simple RelayCommand implementation for ICommand
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
