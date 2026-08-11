using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class MidiSendEditorView : UserControl
{
    public MidiSendEditorViewModel? ViewModel => DataContext as MidiSendEditorViewModel;

    public MidiSendEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }
}
