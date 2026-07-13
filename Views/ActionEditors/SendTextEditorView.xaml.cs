using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class SendTextEditorView : UserControl
{
    public SendTextEditorViewModel? ViewModel => DataContext as SendTextEditorViewModel;

    public SendTextEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
