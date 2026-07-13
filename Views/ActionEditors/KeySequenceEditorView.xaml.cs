using HotKeyManager.Helpers;
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
        KeyPickerListView.ItemsSource = KeyHelper.GetAllKeys();
    }

    public Visibility CollapsedIf(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public Brush? RecordBackground(bool isRecording) =>
        isRecording ? new SolidColorBrush(Colors.IndianRed) : null;

    private void RemoveKeyStroke_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is KeyStroke stroke)
            ViewModel?.RemoveKeyStrokeCommand.Execute(stroke);
    }

    private void KeySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;

        var query = sender.Text?.Trim() ?? string.Empty;
        var allKeys = KeyHelper.GetAllKeys();

        if (string.IsNullOrEmpty(query))
        {
            KeyPickerListView.ItemsSource = allKeys;
            sender.ItemsSource = null;
        }
        else
        {
            var filtered = allKeys
                .Where(k => k.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
            sender.ItemsSource = filtered.Select(k => k.Name).ToList();
            KeyPickerListView.ItemsSource = filtered;
        }
    }

    private void KeySearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is string keyName)
        {
            var entry = KeyHelper.GetAllKeys().FirstOrDefault(k => k.Name == keyName);
            if (entry != null)
            {
                ViewModel?.AddManualKey(entry);
                sender.Text = string.Empty;
                ManualKeyFlyout.Hide();
            }
        }
    }

    private void KeyPickerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (KeyPickerListView.SelectedItem is KeyHelper.VirtualKeyEntry entry)
        {
            ViewModel?.AddManualKey(entry);
            KeyPickerListView.SelectedItem = null;
            KeySearchBox.Text = string.Empty;
            ManualKeyFlyout.Hide();
        }
    }
}
