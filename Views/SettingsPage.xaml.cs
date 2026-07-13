using System.Text.Json;
using HotKeyManager.Models;
using HotKeyManager.Services;
using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HotKeyManager.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; } = new();

    public SettingsPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Load();
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
                    LegacyActionMigration.WrapLegacyActions(config.Hotkeys);
                    App.Current.ConfigurationService.Configuration = config;
                    await App.Current.ConfigurationService.SaveAsync();
                    App.Current.HotkeyManagerService.LoadHotkeys(config.Hotkeys);
                    App.Current.LogService.MinLogLevel = LogService.ParseLogLevel(config.Settings.LogLevel);
                    ThemeService.Apply(config.Settings.Theme);
                    ViewModel.Load();

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
            ThemeService.Apply(App.Current.ConfigurationService.Configuration.Settings.Theme);
            ViewModel.Load();

            await ShowMessage("Zurückgesetzt", "Alle Daten wurden gelöscht.");
        }
    }

    private async void InstallDriver_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsDriverInstalled)
        {
            var confirmDialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = "Treiber deinstallieren?",
                Content = "Der Interception-Treiber wird deinstalliert. Kernel-Mode Tastatur-Injektion ist danach nicht mehr verfügbar.",
                PrimaryButtonText = "Deinstallieren",
                CloseButtonText = "Abbrechen",
                DefaultButton = ContentDialogButton.Close
            };

            if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary) return;

            var (success, message) = await ViewModel.UninstallDriverAsync();
            await ShowMessage(success ? "Deinstallation erfolgreich" : "Deinstallation fehlgeschlagen", message);
        }
        else
        {
            var (success, message) = await ViewModel.InstallDriverAsync();
            await ShowMessage(success ? "Installation erfolgreich" : "Installation fehlgeschlagen", message);
        }
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        var updateService = App.Current.UpdateService;

        if (!updateService.IsSupported)
        {
            await ShowMessage(
                "Update-Check nicht verfügbar",
                "Die App läuft aus einem Build-Verzeichnis. Der Update-Check funktioniert nur in der installierten Version.");
            return;
        }

        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButtonText.Text = "Prüfe…";

        var updateInfo = await updateService.CheckForUpdatesAsync();

        CheckUpdatesButton.IsEnabled = true;
        CheckUpdatesButtonText.Text = "Nach Updates suchen";

        if (updateInfo == null)
        {
            await ShowMessage("Kein Update verfügbar", $"{ViewModel.VersionText} ist aktuell.");
            return;
        }

        var mainViewModel = (App.Current.MainWindow as MainWindow)?.ViewModel;
        mainViewModel?.ShowUpdateAvailable(updateInfo);

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = $"Version {updateInfo.TargetFullRelease.Version} verfügbar",
            Content = "Das Update kann jetzt heruntergeladen und installiert werden. Die App startet danach automatisch neu.",
            PrimaryButtonText = "Jetzt aktualisieren",
            CloseButtonText = "Später",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && mainViewModel != null)
        {
            await mainViewModel.UpdateNowCommand.ExecuteAsync(null);
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
