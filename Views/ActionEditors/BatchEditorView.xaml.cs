using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class BatchEditorView : UserControl
{
    public BatchEditorViewModel? ViewModel => DataContext as BatchEditorViewModel;

    public BatchEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
