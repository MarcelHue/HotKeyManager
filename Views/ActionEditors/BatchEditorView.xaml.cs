using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI for the batch command block.</summary>
public sealed partial class BatchEditorView : UserControl
{
    /// <summary>The editor view model, taken from the DataContext set by the template.</summary>
    public BatchEditorViewModel? ViewModel => this.DataContext as BatchEditorViewModel;

    /// <summary>Creates the view; x:Bind is refreshed whenever the template assigns a new DataContext.</summary>
    public BatchEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (s, e) => this.Bindings.Update();
    }
}
