using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI for the Windows action block.</summary>
public sealed partial class SystemEditorView : UserControl
{
    /// <summary>The editor view model, taken from the DataContext set by the template.</summary>
    public SystemEditorViewModel? ViewModel => this.DataContext as SystemEditorViewModel;

    /// <summary>Creates the view; x:Bind is refreshed whenever the template assigns a new DataContext.</summary>
    public SystemEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (s, e) => this.Bindings.Update();
    }
}
