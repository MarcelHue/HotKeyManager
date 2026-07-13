using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class WebhookEditorView : UserControl
{
    public WebhookEditorViewModel? ViewModel => DataContext as WebhookEditorViewModel;

    public WebhookEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
