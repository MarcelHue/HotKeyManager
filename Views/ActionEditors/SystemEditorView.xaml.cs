using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class SystemEditorView : UserControl
{
    public SystemEditorViewModel? ViewModel => DataContext as SystemEditorViewModel;

    public SystemEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
