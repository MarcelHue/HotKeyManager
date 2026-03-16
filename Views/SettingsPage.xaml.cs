using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using HotKeyManager.Models;
using HotKeyManager.Services;
using System.Text.Json;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.UI;
using WinRT.Interop;

namespace HotKeyManager.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly SolidColorBrush GreenBrush = new(Color.FromArgb(255, 74, 222, 128));
    private static readonly SolidColorBrush YellowBrush = new(Color.FromArgb(255, 250, 204, 21));
    private static readonly SolidColorBrush RedBrush = new(Color.FromArgb(255, 239, 68, 68));

    public SettingsPage()
    {
        this.InitializeComponent();
        LoadSettings();
        UpdateDriverStatus();
    }

    private void LoadSettings()
    {
        AutoStartToggle.Toggled -= AutoStartToggle_Toggled;
        MinimizeToTrayToggle.Toggled -= MinimizeToTrayToggle_Toggled;
        StartMinimizedToggle.Toggled -= StartMinimizedToggle_Toggled;

        var settings = App.Current.ConfigurationService.Configuration.Settings;
        AutoStartToggle.IsOn = App.Current.AutoStartService.IsAutoStartEnabled;
        MinimizeToTrayToggle.IsOn = settings.MinimizeToTray;
        StartMinimizedToggle.IsOn = settings.StartMinimized;

        AutoStartToggle.Toggled += AutoStartToggle_Toggled;
        MinimizeToTrayToggle.Toggled += MinimizeToTrayToggle_Toggled;
        StartMinimizedToggle.Toggled += StartMinimizedToggle_Toggled;
    }

    private void UpdateDriverStatus()
    {
        var installed = InterceptionService.IsDriverInstalled();
        var active = installed && InterceptionService.IsDriverActive();

        if (active)
        {
            DriverStatusDot.Fill = GreenBrush;
            DriverStatusLabel.Text = "Aktiv";
            DriverActionIcon.Glyph = "\uE74D";
            DriverActionText.Text = "Deinstallieren";
            DriverActionButton.Background = new SolidColorBrush(Color.FromArgb(255, 127, 29, 29));
        }
        else if (installed)
        {
            DriverStatusDot.Fill = YellowBrush;
            DriverStatusLabel.Text = "Installiert (Neustart noetig)";
            DriverActionIcon.Glyph = "\uE74D";
            DriverActionText.Text = "Deinstallieren";
            DriverActionButton.Background = new SolidColorBrush(Color.FromArgb(255, 127, 29, 29));
        }
        else
        {
            DriverStatusDot.Fill = RedBrush;
            DriverStatusLabel.Text = "Nicht installiert";
            DriverActionIcon.Glyph = "\uE896";
            DriverActionText.Text = "Installieren";
            DriverActionButton.Background = null;
        }
    }

    private async void DriverAction_Click(object sender, RoutedEventArgs e)
    {
        var installed = InterceptionService.IsDriverInstalled();

        if (installed)
        {
            var confirm = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Treiber deinstallieren?",
                Content = "Der Interception-Treiber wird deinstalliert. Kernel-Mode Tastatur-Injektion ist danach nicht mehr verfuegbar.",
                PrimaryButtonText = "Deinstallieren",
                CloseButtonText = "Abbrechen",
                DefaultButton = ContentDialogButton.Close
            };

            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            DriverActionButton.IsEnabled = false;
            var (success, message) = await InterceptionService.UninstallDriverAsync();
            DriverActionButton.IsEnabled = true;

            await ShowMessage(success ? "Deinstallation erfolgreich" : "Deinstallation fehlgeschlagen", message);
        }
        else
        {
            DriverActionButton.IsEnabled = false;
            var (success, message) = await InterceptionService.InstallDriverAsync();
            DriverActionButton.IsEnabled = true;

            if (success && InterceptionService.IsDriverActive())
            {
                App.Current.InterceptionService.Start();
            }

            await ShowMessage(success ? "Installation erfolgreich" : "Installation fehlgeschlagen", message);
        }

        UpdateDriverStatus();
        (App.Current.MainWindow as MainWindow)?.UpdateStatusDisplay();
    }

    private async void SaveSettings()
    {
        await App.Current.ConfigurationService.SaveAsync();
    }

    private void AutoStartToggle_Toggled(object sender, RoutedEventArgs e)
    {
        var success = App.Current.AutoStartService.SetAutoStart(AutoStartToggle.IsOn);
        if (!success)
        {
            AutoStartToggle.Toggled -= AutoStartToggle_Toggled;
            AutoStartToggle.IsOn = !AutoStartToggle.IsOn;
            AutoStartToggle.Toggled += AutoStartToggle_Toggled;
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
