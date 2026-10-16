using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI of the "MIDI senden" block, including the preset dialogs.</summary>
public sealed partial class MidiSendEditorView : UserControl
{
    /// <summary>Refreshes the x:Bind bindings whenever the block behind the view changes.</summary>
    public MidiSendEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (_, _) => this.Bindings.Update();
    }

    public MidiSendEditorViewModel? ViewModel => this.DataContext as MidiSendEditorViewModel;

    private async void ImportPreset_Click(object sender, RoutedEventArgs e)
    {
        if (this.ViewModel is null || App.Current.MainWindow is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.Current.MainWindow));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var success = this.ViewModel.PresetPicker.Import(file.Path);
        await this.ShowResultAsync(
            success,
            "Preset importiert",
            "Das Preset steht jetzt in der Auswahl zur Verfügung.",
            "Die Datei ist kein gültiges MIDI-Preset (erwartet wird eine JSON-Datei mit \"name\" und \"commands\").",
            "Import fehlgeschlagen");
    }

    private async void SaveAsNewCommand_Click(object sender, RoutedEventArgs e)
    {
        if (this.ViewModel is null)
        {
            return;
        }

        var input = new TextBox { PlaceholderText = "z.B. Kanal 1 stumm umschalten" };
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Als Preset-Eintrag speichern",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Der Eintrag wird mit den aktuellen Werten gespeichert und als verifiziert markiert.",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    input,
                },
            },
            PrimaryButtonText = "Speichern",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        await this.ShowResultAsync(
            this.ViewModel.SaveAsNewCommand(input.Text),
            "Eintrag gespeichert",
            "Der Befehl steht jetzt im Preset zur Verfügung.",
            "Der Eintrag konnte nicht gespeichert werden. Bitte gib einen Namen ein.");
    }

    private async void UpdateCommand_Click(object sender, RoutedEventArgs e)
    {
        if (this.ViewModel is null)
        {
            return;
        }

        await this.ShowResultAsync(
            this.ViewModel.UpdateSelectedCommand(),
            "Eintrag aktualisiert",
            "Der Preset-Eintrag wurde mit den aktuellen Werten überschrieben und als verifiziert markiert.",
            "Der Eintrag konnte nicht aktualisiert werden.");
    }

    private async void OpenPresetFile_Click(object sender, RoutedEventArgs e)
    {
        if (this.ViewModel is null)
        {
            return;
        }

        if (!this.ViewModel.PresetPicker.OpenSelectedFile())
        {
            const string message = "Die Preset-Datei konnte nicht geöffnet werden.";
            await this.ShowResultAsync(false, string.Empty, string.Empty, message);
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Preset-Datei geöffnet",
            Content = "Die JSON-Datei wurde im Standardprogramm geöffnet. "
                + "Speichere dort deine Änderungen und klicke anschließend auf „Neu laden“.",
            PrimaryButtonText = "Neu laden",
            CloseButtonText = "Später",
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            this.ViewModel.PresetPicker.ReloadFromDisk();
        }
    }

    private async Task ShowResultAsync(
        bool success,
        string successTitle,
        string successMessage,
        string errorMessage,
        string errorTitle = "Fehlgeschlagen")
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = success ? successTitle : errorTitle,
            Content = success ? successMessage : errorMessage,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }
}
