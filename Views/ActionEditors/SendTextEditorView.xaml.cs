using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI for the text macro block.</summary>
public sealed partial class SendTextEditorView : UserControl
{
    /// <summary>The editor view model, taken from the DataContext set by the template.</summary>
    public SendTextEditorViewModel? ViewModel => this.DataContext as SendTextEditorViewModel;

    /// <summary>Creates the view; x:Bind is refreshed whenever the template assigns a new DataContext.</summary>
    public SendTextEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (s, e) => this.Bindings.Update();
    }
}
