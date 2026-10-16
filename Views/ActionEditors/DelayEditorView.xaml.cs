using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI for the delay block.</summary>
public sealed partial class DelayEditorView : UserControl
{
    /// <summary>The editor view model, taken from the DataContext set by the template.</summary>
    public DelayEditorViewModel? ViewModel => this.DataContext as DelayEditorViewModel;

    /// <summary>Creates the view; x:Bind is refreshed whenever the template assigns a new DataContext.</summary>
    public DelayEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (s, e) => this.Bindings.Update();
    }
}
