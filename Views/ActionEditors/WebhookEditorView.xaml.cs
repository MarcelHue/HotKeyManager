using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI for the webhook block.</summary>
public sealed partial class WebhookEditorView : UserControl
{
    /// <summary>The editor view model, taken from the DataContext set by the template.</summary>
    public WebhookEditorViewModel? ViewModel => this.DataContext as WebhookEditorViewModel;

    /// <summary>Creates the view; x:Bind is refreshed whenever the template assigns a new DataContext.</summary>
    public WebhookEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (s, e) => this.Bindings.Update();
    }
}
