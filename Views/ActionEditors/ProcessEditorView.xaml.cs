using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI for the block that starts a program.</summary>
public sealed partial class ProcessEditorView : UserControl
{
    /// <summary>Creates the view; x:Bind is refreshed whenever the template assigns a new DataContext.</summary>
    public ProcessEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (s, e) => this.Bindings.Update();
    }

    /// <summary>The editor view model, taken from the DataContext set by the template.</summary>
    public ProcessEditorViewModel? ViewModel => this.DataContext as ProcessEditorViewModel;

    private async void BrowseFile_Click(object sender, RoutedEventArgs e)
    {
        if (this.ViewModel is null || App.Current.MainWindow is null)
        {
            return;
        }

        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");

        // WinUI 3 desktop apps must attach pickers to a window handle.
        var hwnd = WindowNative.GetWindowHandle(App.Current.MainWindow);
        InitializeWithWindow.Initialize(picker, hwnd);

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            this.ViewModel.FilePath = file.Path;
        }
    }
}
