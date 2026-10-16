using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI for the key sequence block, including the manual key picker.</summary>
public sealed partial class KeySequenceEditorView : UserControl
{
    /// <summary>Creates the view; x:Bind is refreshed whenever the template assigns a new DataContext.</summary>
    public KeySequenceEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (s, e) => this.Bindings.Update();
        this.KeyPickerListView.ItemsSource = KeyHelper.GetAllKeys();
    }

    /// <summary>The editor view model, taken from the DataContext set by the template.</summary>
    public KeySequenceEditorViewModel? ViewModel => this.DataContext as KeySequenceEditorViewModel;

    /// <summary>x:Bind helper: collapses an element when the value is true.</summary>
    public Visibility CollapsedIf(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>x:Bind helper: highlights the record button while a recording runs.</summary>
    public Brush? RecordBackground(bool isRecording) => isRecording ? new SolidColorBrush(Colors.IndianRed) : null;

    private void RemoveKeyStroke_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: KeyStroke stroke })
        {
            this.ViewModel?.RemoveKeyStrokeCommand.Execute(stroke);
        }
    }

    private void KeySearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        var query = sender.Text?.Trim() ?? string.Empty;
        var allKeys = KeyHelper.GetAllKeys();

        if (string.IsNullOrEmpty(query))
        {
            this.KeyPickerListView.ItemsSource = allKeys;
            sender.ItemsSource = null;
            return;
        }

        var filtered = allKeys
            .Where(k => k.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        sender.ItemsSource = filtered.Select(k => k.Name).ToList();
        this.KeyPickerListView.ItemsSource = filtered;
    }

    private void KeySearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is not string keyName)
        {
            return;
        }

        var entry = KeyHelper.GetAllKeys().FirstOrDefault(k => k.Name == keyName);
        if (entry is null)
        {
            return;
        }

        this.ViewModel?.AddManualKey(entry);
        sender.Text = string.Empty;
        this.ManualKeyFlyout.Hide();
    }

    private void KeyPickerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (this.KeyPickerListView.SelectedItem is not KeyHelper.VirtualKeyEntry entry)
        {
            return;
        }

        this.ViewModel?.AddManualKey(entry);
        this.KeyPickerListView.SelectedItem = null;
        this.KeySearchBox.Text = string.Empty;
        this.ManualKeyFlyout.Hide();
    }
}
