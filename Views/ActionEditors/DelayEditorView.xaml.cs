using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class DelayEditorView : UserControl
{
    public DelayEditorViewModel? ViewModel => DataContext as DelayEditorViewModel;

    public DelayEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
