using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class MidiSendEditorView : UserControl
{
    public MidiSendEditorViewModel? ViewModel => DataContext as MidiSendEditorViewModel;

    public MidiSendEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }

    private async void ImportPreset_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null || App.Current.MainWindow == null) return;

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");

        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return;

        var success = ViewModel.ImportPreset(file.Path);

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = success ? "Preset importiert" : "Import fehlgeschlagen",
            Content = success
                ? "Das Preset steht jetzt in der Auswahl zur Verfügung."
                : "Die Datei ist kein gültiges MIDI-Preset (erwartet wird eine JSON-Datei mit \"name\" und \"commands\").",
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }

    private async void SaveAsNewCommand_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

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
                        TextWrapping = TextWrapping.Wrap
                    },
                    input
                }
            },
            PrimaryButtonText = "Speichern",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var saved = ViewModel.SaveAsNewCommand(input.Text);
        await ShowResult(saved,
            "Eintrag gespeichert",
            "Der Befehl steht jetzt im Preset zur Verfügung.",
            "Der Eintrag konnte nicht gespeichert werden. Bitte gib einen Namen ein.");
    }

    private async void UpdateCommand_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var updated = ViewModel.UpdateSelectedCommand();
        await ShowResult(updated,
            "Eintrag aktualisiert",
            "Der Preset-Eintrag wurde mit den aktuellen Werten überschrieben und als verifiziert markiert.",
            "Der Eintrag konnte nicht aktualisiert werden.");
    }

    private async Task ShowResult(bool success, string successTitle, string successMessage, string errorMessage)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = success ? successTitle : "Fehlgeschlagen",
            Content = success ? successMessage : errorMessage,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
