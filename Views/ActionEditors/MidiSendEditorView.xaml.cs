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
}
