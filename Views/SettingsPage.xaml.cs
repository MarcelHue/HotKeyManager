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

/// <summary>Settings page: appearance, startup, logging, driver, configuration file and updates.</summary>
public sealed partial class SettingsPage : Page
{
    private const string CheckUpdatesText = "Nach Updates suchen";

    /// <summary>Creates the page.</summary>
    public SettingsPage()
    {
        this.InitializeComponent();
    }

    /// <summary>The settings view model.</summary>
    public SettingsViewModel ViewModel { get; } = new();

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        this.ViewModel.Load();
    }

    private static void AttachToMainWindow(object picker)
    {
        // WinUI 3 desktop apps must attach pickers to a window handle.
        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async void ExportConfig_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = "hotkey-config"
        };
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        AttachToMainWindow(picker);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        var config = App.Current.ConfigurationService.Configuration;
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await FileIO.WriteTextAsync(file, json);

        await this.ShowMessageAsync("Export erfolgreich", "Die Konfiguration wurde exportiert.");
    }

    private async void ImportConfig_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".json");
        AttachToMainWindow(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        try
        {
            var json = await FileIO.ReadTextAsync(file);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json);
            if (config is null)
            {
                return;
            }

            await this.ApplyImportedConfigurationAsync(config);
            await this.ShowMessageAsync("Import erfolgreich", $"{config.Hotkeys.Count} Hotkeys wurden importiert.");
        }
        catch (Exception ex)
        {
            await this.ShowMessageAsync(
                "Fehler beim Import",
                $"Die Datei konnte nicht gelesen werden: {ex.Message}");
        }
    }

    private async Task ApplyImportedConfigurationAsync(AppConfiguration config)
    {
        LegacyActionMigration.WrapLegacyActions(config.Hotkeys);
        App.Current.ConfigurationService.Configuration = config;
        await App.Current.ConfigurationService.SaveAsync();
        App.Current.HotkeyManagerService.LoadHotkeys(config.Hotkeys);
        App.Current.LogService.MinLogLevel = LogService.ParseLogLevel(config.Settings.LogLevel);
        ThemeService.Apply(config.Settings.Theme);
        this.ViewModel.Load();
    }

    private async void ResetConfig_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Alle Daten löschen?",
            Content = "Diese Aktion kann nicht rückgängig gemacht werden. "
                + "Alle Hotkeys und Einstellungen werden gelöscht.",
            PrimaryButtonText = "Löschen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await App.Current.ConfigurationService.ResetAsync();
        App.Current.HotkeyManagerService.LoadHotkeys(new List<HotkeyDefinition>());
        ThemeService.Apply(App.Current.ConfigurationService.Configuration.Settings.Theme);
        this.ViewModel.Load();

        await this.ShowMessageAsync("Zurückgesetzt", "Alle Daten wurden gelöscht.");
    }

    private async void InstallDriver_Click(object sender, RoutedEventArgs e)
    {
        if (!this.ViewModel.IsDriverInstalled)
        {
            var (installed, installMessage) = await this.ViewModel.InstallDriverAsync();
            var installTitle = installed ? "Installation erfolgreich" : "Installation fehlgeschlagen";
            await this.ShowMessageAsync(installTitle, installMessage);
            return;
        }

        var confirmDialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Treiber deinstallieren?",
            Content = "Der Interception-Treiber wird deinstalliert. "
                + "Kernel-Mode Tastatur-Injektion ist danach nicht mehr verfügbar.",
            PrimaryButtonText = "Deinstallieren",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close
        };

        if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var (uninstalled, uninstallMessage) = await this.ViewModel.UninstallDriverAsync();
        var uninstallTitle = uninstalled ? "Deinstallation erfolgreich" : "Deinstallation fehlgeschlagen";
        await this.ShowMessageAsync(uninstallTitle, uninstallMessage);
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        var updateService = App.Current.UpdateService;

        if (!updateService.IsSupported)
        {
            await this.ShowMessageAsync(
                "Update-Check nicht verfügbar",
                "Die App läuft aus einem Build-Verzeichnis. "
                    + "Der Update-Check funktioniert nur in der installierten Version.");
            return;
        }

        this.CheckUpdatesButton.IsEnabled = false;
        this.CheckUpdatesButtonText.Text = "Prüfe…";

        var updateInfo = await updateService.CheckForUpdatesAsync();

        this.CheckUpdatesButton.IsEnabled = true;
        this.CheckUpdatesButtonText.Text = CheckUpdatesText;

        if (updateInfo is null)
        {
            await this.ShowMessageAsync("Kein Update verfügbar", $"{this.ViewModel.VersionText} ist aktuell.");
            return;
        }

        var mainViewModel = App.Current.MainWindow is MainWindow mainWindow ? mainWindow.ViewModel : null;
        mainViewModel?.ShowUpdateAvailable(updateInfo);

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = $"Version {updateInfo.TargetFullRelease.Version} verfügbar",
            Content = "Das Update kann jetzt heruntergeladen und installiert werden. "
                + "Die App startet danach automatisch neu.",
            PrimaryButtonText = "Jetzt aktualisieren",
            CloseButtonText = "Später",
            DefaultButton = ContentDialogButton.Primary
        };

        if ((await dialog.ShowAsync() == ContentDialogResult.Primary) && (mainViewModel is not null))
        {
            await mainViewModel.UpdateNowCommand.ExecuteAsync(null);
        }
    }

    private async Task ShowMessageAsync(string title, string message)
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
