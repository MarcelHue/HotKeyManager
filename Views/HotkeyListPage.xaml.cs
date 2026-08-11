using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace HotKeyManager.Views;

public sealed partial class HotkeyListPage : Page
{
    public HotkeyListViewModel ViewModel { get; } = new();

    private HotkeyDefinition? _draggedHotkey;
    private HotkeyGroup? _dragSourceGroup;
    private bool _crossGroupDropHandled;

    public HotkeyListPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Activate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Deactivate();
    }

    private static HotkeyDefinition? HotkeyOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as HotkeyDefinition;

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ViewModel.SearchText = sender.Text;
    }

    #region Neu / Bearbeiten / Kopieren / Löschen

    private void AddHotkey_Click(object sender, object e)
    {
        Frame.Navigate(typeof(HotkeyEditorPage), new EditorNavArgs());
    }

    private void AddTextMacro_Click(object sender, RoutedEventArgs e)
    {
        // Legt ein Makro mit vorbereitetem Text-Baustein an
        Frame.Navigate(typeof(HotkeyEditorPage), new EditorNavArgs { PreselectType = ActionType.SendText });
    }

    private void EditHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
            Frame.Navigate(typeof(HotkeyEditorPage), new EditorNavArgs { Hotkey = hotkey });
    }

    private void DuplicateHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey)
            return;

        // Tiefe Kopie, damit Original und Kopie keine Bausteine teilen
        var copy = new HotkeyDefinition
        {
            Name = hotkey.Name,
            Group = hotkey.Group,
            Action = ActionCloner.Clone(hotkey.Action),
            WindowMode = hotkey.WindowMode,
            TargetProcessName = hotkey.TargetProcessName,
            TargetWindowTitle = hotkey.TargetWindowTitle
        };

        Frame.Navigate(typeof(HotkeyEditorPage), new EditorNavArgs { Hotkey = copy, IsDuplicate = true });
    }

    private async void RunHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
            await ViewModel.RunCommand.ExecuteAsync(hotkey);
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        // OneWay-Binding: der Handler uebernimmt den Wert ins Model und persistiert.
        // Feuert der Event nur durch Container-Recycling, sind Werte gleich -> kein Save.
        if (sender is ToggleSwitch { DataContext: HotkeyDefinition hotkey } toggle
            && hotkey.IsEnabled != toggle.IsOn)
        {
            hotkey.IsEnabled = toggle.IsOn;
            ViewModel.PersistToggle();
        }
    }

    private async void DeleteHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey)
            return;

        var confirmDialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Hotkey löschen?",
            Content = $"Möchtest du den Hotkey \"{hotkey.Name}\" wirklich löschen?",
            PrimaryButtonText = "Löschen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close
        };

        var result = await confirmDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.Delete(hotkey);
        }
    }

    #endregion

    #region Inline-Umbenennen

    private void Name_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
            ViewModel.BeginRename(hotkey);
    }

    private void RenameHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
            ViewModel.BeginRename(hotkey);
    }

    private void RenameBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box) return;

        box.Focus(FocusState.Programmatic);
        box.SelectAll();
    }

    private void RenameBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey) return;

        if (e.Key == VirtualKey.Enter)
        {
            ViewModel.CommitRename(hotkey);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            ViewModel.CancelRename(hotkey);
            e.Handled = true;
        }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
            ViewModel.CommitRename(hotkey);
    }

    #endregion

    #region Auslöser erfassen

    private void CaptureKey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
            ViewModel.StartKeyCapture(hotkey);
    }

    private void CaptureMidi_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
            ViewModel.StartMidiLearn(hotkey);
    }

    private async void ClearKey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey) return;

        if (hotkey.MidiTrigger == null)
        {
            await ShowMessage("Nicht möglich",
                "Der Hotkey hätte danach keinen Auslöser mehr. Erfasse zuerst einen MIDI-Trigger.");
            return;
        }

        ViewModel.ClearKeyTrigger(hotkey);
    }

    private async void ClearMidi_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey) return;

        if (hotkey.VirtualKeyCode == 0)
        {
            await ShowMessage("Nicht möglich",
                "Der Hotkey hätte danach keinen Auslöser mehr. Erfasse zuerst eine Tastenkombination.");
            return;
        }

        ViewModel.ClearMidiTrigger(hotkey);
    }

    #endregion

    #region Ordner zuweisen

    private void AssignGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || HotkeyOf(sender) is not { } hotkey) return;

        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };

        var noGroup = new MenuFlyoutItem { Text = HotkeyGroup.NoGroupLabel };
        noGroup.Click += (_, _) => ViewModel.SetGroup(hotkey, string.Empty);
        flyout.Items.Add(noGroup);

        if (ViewModel.KnownGroups.Count > 0)
            flyout.Items.Add(new MenuFlyoutSeparator());

        foreach (var group in ViewModel.KnownGroups)
        {
            var item = new MenuFlyoutItem { Text = group };
            var target = group;
            item.Click += (_, _) => ViewModel.SetGroup(hotkey, target);
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var newGroup = new MenuFlyoutItem
        {
            Text = "Neuer Ordner…",
            Icon = new FontIcon { Glyph = "" }
        };
        newGroup.Click += async (_, _) => await CreateGroupAsync(hotkey);
        flyout.Items.Add(newGroup);

        flyout.ShowAt(button);
    }

    private async Task CreateGroupAsync(HotkeyDefinition hotkey)
    {
        var input = new TextBox { PlaceholderText = "z.B. Rode Bank 1" };

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = "Neuer Ordner",
            Content = input,
            PrimaryButtonText = "Zuweisen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        var name = input.Text?.Trim();
        if (!string.IsNullOrEmpty(name))
            ViewModel.SetGroup(hotkey, name);
    }

    #endregion

    #region Drag & Drop

    private void Items_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (!ViewModel.IsReorderEnabled)
        {
            // Bei aktiver Suche waere die resultierende Gesamtreihenfolge nicht nachvollziehbar
            e.Cancel = true;
            return;
        }

        _draggedHotkey = e.Items.FirstOrDefault() as HotkeyDefinition;
        _dragSourceGroup = (sender as FrameworkElement)?.DataContext as HotkeyGroup;
        _crossGroupDropHandled = false;

        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private void Group_DragOver(object sender, DragEventArgs e)
    {
        if (_draggedHotkey == null) return;

        e.AcceptedOperation = DataPackageOperation.Move;
        e.Handled = true;
    }

    private void Group_Drop(object sender, DragEventArgs e)
    {
        if (_draggedHotkey is not { } hotkey) return;
        if ((sender as FrameworkElement)?.DataContext is not HotkeyGroup targetGroup) return;

        // Innerhalb derselben Gruppe erledigt die ListView das Umsortieren selbst;
        // die neue Reihenfolge wird in DragItemsCompleted uebernommen.
        if (targetGroup == _dragSourceGroup) return;

        ViewModel.MoveToGroup(hotkey, targetGroup);

        _crossGroupDropHandled = true;
        _draggedHotkey = null;
        _dragSourceGroup = null;
        e.Handled = true;
    }

    private void Items_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (!_crossGroupDropHandled)
            ViewModel.ApplyOrderFromGroups();

        _draggedHotkey = null;
        _dragSourceGroup = null;
        _crossGroupDropHandled = false;
    }

    #endregion

    private async Task ShowMessage(string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };
        await dialog.ShowAsync();
    }
}
