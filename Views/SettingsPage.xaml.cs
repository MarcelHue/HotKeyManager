using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HotKeyManager.Models;
using HotKeyManager.Services;
using System.Text.Json;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;

namespace HotKeyManager.Views;

public sealed partial class SettingsPage : Page
{
    private bool _isLoading = true;
    
    public SettingsPage()
    {
        this.InitializeComponent();
        LoadSettings();
    }
    
    private void LoadSettings()
    {
        var settings = App.Current.ConfigurationService.Configuration.Settings;
        
        AutoStartToggle.IsOn = App.Current.AutoStartService.IsAutoStartEnabled;
        MinimizeToTrayToggle.IsOn = settings.MinimizeToTray;
        StartMinimizedToggle.IsOn = settings.StartMinimized;
        
        UpdateDriverStatus();
        
        _isLoading = false;
    }
    
    private void UpdateDriverStatus()
    {
        var isInstalled = InterceptionService.IsDriverInstalled();
        var isActive = InterceptionService.IsDriverActive();
        
        if (isInstalled && isActive)
        {
            // Treiber installiert UND aktiv
            DriverStatusIcon.Glyph = "\uE73E"; // Checkmark
            DriverStatusIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
            DriverStatusText.Text = "Aktiv";
            InstallDriverButtonText.Text = "Treiber deinstallieren";
            DriverInfoBar.Severity = InfoBarSeverity.Success;
            DriverInfoBar.Title = "Treiber aktiv";
            DriverInfoBar.Message = "Der Interception-Treiber ist installiert und aktiv. Kernel-Mode Interception ist verfügbar.";
        }
        else if (isInstalled && !isActive)
        {
            // Treiber installiert aber NICHT aktiv (Neustart erforderlich)
            DriverStatusIcon.Glyph = "\uE7BA"; // Warning
            DriverStatusIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            DriverStatusText.Text = "Neustart erforderlich";
            InstallDriverButtonText.Text = "Treiber deinstallieren";
            DriverInfoBar.Severity = InfoBarSeverity.Warning;
            DriverInfoBar.Title = "Neustart erforderlich";
            DriverInfoBar.Message = "Der Treiber ist installiert, aber noch nicht aktiv. Bitte starte den Computer neu, um die Kernel-Mode Interception zu aktivieren.";
        }
        else
        {
            // Treiber nicht installiert
            DriverStatusIcon.Glyph = "\uE711"; // Cancel/X
            DriverStatusIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.IndianRed);
            DriverStatusText.Text = "Nicht installiert";
            InstallDriverButtonText.Text = "Treiber installieren";
            DriverInfoBar.Severity = InfoBarSeverity.Informational;
            DriverInfoBar.Title = "Hinweis";
            DriverInfoBar.Message = "Die Installation erfordert Administrator-Rechte. Nach der Installation ist ein Neustart des Computers erforderlich.";
        }
    }
    
    private async void SaveSettings()
    {
        if (_isLoading) return;
        await App.Current.ConfigurationService.SaveAsync();
    }
    
    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;
        
        var success = App.Current.AutoStartService.SetAutoStart(AutoStartToggle.IsOn);
        if (!success)
        {
            _isLoading = true;
            AutoStartToggle.IsOn = !AutoStartToggle.IsOn;
            _isLoading = false;
        }
        
        App.Current.ConfigurationService.Configuration.Settings.RunAtStartup = AutoStartToggle.IsOn;
        SaveSettings();
    }
    
    private void MinimizeToTrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        App.Current.ConfigurationService.Configuration.Settings.MinimizeToTray = MinimizeToTrayToggle.IsOn;
        SaveSettings();
    }
    
    private void StartMinimizedToggle_Toggled(object sender, RoutedEventArgs e)
    {
        App.Current.ConfigurationService.Configuration.Settings.StartMinimized = StartMinimizedToggle.IsOn;
        SaveSettings();
    }
    
    private async void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "hotkey-config"
        };
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        
        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        
        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            var config = App.Current.ConfigurationService.Configuration;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await FileIO.WriteTextAsync(file, json);
            
            await ShowMessage("Export erfolgreich", "Die Konfiguration wurde exportiert.");
        }
    }
    
    private async void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".json");
        
        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
        
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            try
            {
                var json = await FileIO.ReadTextAsync(file);
                var config = JsonSerializer.Deserialize<AppConfiguration>(json);
                
                if (config != null)
                {
                    App.Current.ConfigurationService.Configuration = config;
                    await App.Current.ConfigurationService.SaveAsync();
                    App.Current.HotkeyManagerService.LoadHotkeys(config.Hotkeys);
                    LoadSettings();
                    
                    await ShowMessage("Import erfolgreich", $"{config.Hotkeys.Count} Hotkeys wurden importiert.");
                }
            }
            catch (Exception ex)
            {
                await ShowMessage("Fehler beim Import", $"Die Datei konnte nicht gelesen werden: {ex.Message}");
            }
        }
    }
    
    private async void ResetConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Alle Daten löschen?",
            Content = "Diese Aktion kann nicht rückgängig gemacht werden. Alle Hotkeys und Einstellungen werden gelöscht.",
            PrimaryButtonText = "Löschen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close
        };
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            await App.Current.ConfigurationService.ResetAsync();
            App.Current.HotkeyManagerService.LoadHotkeys(new List<HotkeyDefinition>());
            LoadSettings();
            
            await ShowMessage("Zurückgesetzt", "Alle Daten wurden gelöscht.");
        }
    }
    
    private async void InstallDriver_Click(object sender, RoutedEventArgs e)
    {
        var isInstalled = InterceptionService.IsDriverInstalled();
        
        if (isInstalled)
        {
            // Deinstallieren
            var confirmDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Treiber deinstallieren?",
                Content = "Der Interception-Treiber wird deinstalliert. Nach der Deinstallation ist ein Neustart erforderlich. Hotkeys mit Kernel-Mode Interception funktionieren dann nicht mehr.",
                PrimaryButtonText = "Deinstallieren",
                CloseButtonText = "Abbrechen",
                DefaultButton = ContentDialogButton.Close
            };
            
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;
            
            InstallDriverButton.IsEnabled = false;
            InstallDriverButtonText.Text = "Wird deinstalliert...";
            
            var (success, message) = await InterceptionService.UninstallDriverAsync();
            
            InstallDriverButton.IsEnabled = true;
            UpdateDriverStatus();
            UpdateMainWindowDriverStatus();
            
            if (success)
            {
                await ShowRestartMessage("Treiber deinstalliert", message);
            }
            else
            {
                await ShowMessage("Fehler", message);
            }
        }
        else
        {
            // Installieren
            var confirmDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Treiber installieren?",
                Content = "Der Interception-Treiber wird installiert. Dies erfordert Administrator-Rechte. Nach der Installation ist ein Neustart des Computers erforderlich.",
                PrimaryButtonText = "Installieren",
                CloseButtonText = "Abbrechen",
                DefaultButton = ContentDialogButton.Primary
            };
            
            var result = await confirmDialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;
            
            InstallDriverButton.IsEnabled = false;
            InstallDriverButtonText.Text = "Wird installiert...";
            
            var (success, message) = await InterceptionService.InstallDriverAsync();
            
            InstallDriverButton.IsEnabled = true;
            UpdateDriverStatus();
            UpdateMainWindowDriverStatus();
            
            if (success)
            {
                await ShowRestartMessage("Treiber installiert", message);
            }
            else
            {
                await ShowMessage("Fehler", message);
            }
        }
    }
    
    private void UpdateMainWindowDriverStatus()
    {
        if (App.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.UpdateDriverStatus();
        }
    }
    
    private async Task ShowRestartMessage(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = title,
            Content = message + "\n\nMöchtest du den Computer jetzt neu starten?",
            PrimaryButtonText = "Jetzt neu starten",
            CloseButtonText = "Später",
            DefaultButton = ContentDialogButton.Close
        };
        
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            // System neu starten
            System.Diagnostics.Process.Start("shutdown", "/r /t 0");
        }
    }
    
    private async Task ShowMessage(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
