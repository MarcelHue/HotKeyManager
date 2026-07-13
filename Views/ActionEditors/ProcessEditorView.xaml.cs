using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class ProcessEditorView : UserControl
{
    public ProcessEditorViewModel? ViewModel => DataContext as ProcessEditorViewModel;

    public ProcessEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null || App.Current.MainWindow == null) return;

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            ViewModel.FilePath = file.Path;
        }
    }
}
