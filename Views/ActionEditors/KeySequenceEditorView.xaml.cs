using HotKeyManager.Models;
using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class KeySequenceEditorView : UserControl
{
    public KeySequenceEditorViewModel? ViewModel => DataContext as KeySequenceEditorViewModel;

    public KeySequenceEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }

    public Visibility CollapsedIf(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public Brush? RecordBackground(bool isRecording) =>
        isRecording ? new SolidColorBrush(Colors.IndianRed) : null;

    private void RemoveKeyStroke_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is KeyStroke stroke)
            ViewModel?.RemoveKeyStrokeCommand.Execute(stroke);
    }
}
