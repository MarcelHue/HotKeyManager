using HotKeyManager.Helpers;
using HotKeyManager.Models;
using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace HotKeyManager.Views;

/// <summary>The hotkey list: folders, inline rename, trigger capture, menus and drag and drop.</summary>
public sealed partial class HotkeyListPage : Page
{
    private const string NotPossibleTitle = "Nicht möglich";

    private readonly HotkeyRowDragDrop dragDrop;

    /// <summary>Builds the page; the list loads when it is navigated to.</summary>
    public HotkeyListPage()
    {
        this.InitializeComponent();
        this.dragDrop = new HotkeyRowDragDrop(this.ViewModel);
    }

    public HotkeyListViewModel ViewModel { get; } = new();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        this.ViewModel.Activate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        this.ViewModel.Deactivate();
    }

    private static HotkeyDefinition? HotkeyOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as HotkeyDefinition;

    private static HotkeyGroup? GroupOf(object sender) => (sender as FrameworkElement)?.DataContext as HotkeyGroup;

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) =>
        this.ViewModel.SearchText = sender.Text;

    private void AddHotkey_Click(object sender, object e) => this.OpenEditor(new EditorNavArgs());

    /// <summary>Creates a macro with a prepared text block.</summary>
    private void AddTextMacro_Click(object sender, RoutedEventArgs e) =>
        this.OpenEditor(new EditorNavArgs { PreselectType = ActionType.SendText });

    private void EditHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
        {
            this.OpenEditor(new EditorNavArgs { Hotkey = hotkey });
        }
    }

    private void DuplicateHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey)
        {
            return;
        }

        // Deep copy, so original and copy do not share blocks.
        var copy = new HotkeyDefinition
        {
            Name = hotkey.Name,
            Group = hotkey.Group,
            Action = ActionCloner.Clone(hotkey.Action),
            WindowMode = hotkey.WindowMode,
            TargetProcessName = hotkey.TargetProcessName,
            TargetWindowTitle = hotkey.TargetWindowTitle,
        };
        this.OpenEditor(new EditorNavArgs { Hotkey = copy, IsDuplicate = true });
    }

    private void OpenEditor(EditorNavArgs args) => this.Frame.Navigate(typeof(HotkeyEditorPage), args);

    private async void RunHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
        {
            await this.ViewModel.RunCommand.ExecuteAsync(hotkey);
        }
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        // The binding is one-way: the handler copies the value into the model and saves.
        // When container recycling fires the event, the values are equal and nothing is saved.
        if (sender is ToggleSwitch { DataContext: HotkeyDefinition hotkey } toggle && hotkey.IsEnabled != toggle.IsOn)
        {
            hotkey.IsEnabled = toggle.IsOn;
            this.ViewModel.PersistToggle();
        }
    }

    private async void DeleteHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey && await HotkeyListDialogs.ConfirmDeleteAsync(this.XamlRoot, hotkey.Name))
        {
            this.ViewModel.Delete(hotkey);
        }
    }

    private void Name_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => this.BeginRename(sender);

    private void RenameHotkey_Click(object sender, RoutedEventArgs e) => this.BeginRename(sender);

    private void BeginRename(object sender)
    {
        if (HotkeyOf(sender) is { } hotkey)
        {
            this.ViewModel.Rename.Begin(hotkey);
        }
    }

    private void RenameBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        }
    }

    private void RenameBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey)
        {
            return;
        }

        if (e.Key == VirtualKey.Enter)
        {
            this.ViewModel.Rename.Commit(hotkey);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Escape)
        {
            this.ViewModel.Rename.Cancel(hotkey);
            e.Handled = true;
        }
    }

    private void RenameBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
        {
            this.ViewModel.Rename.Commit(hotkey);
        }
    }

    private void CaptureKey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
        {
            this.ViewModel.Triggers.StartKey(hotkey);
        }
    }

    private void CaptureMidi_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is { } hotkey)
        {
            this.ViewModel.Triggers.StartMidi(hotkey);
        }
    }

    private async void ClearKey_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey)
        {
            return;
        }

        if (!ListTriggerCapture.CanClearKey(hotkey))
        {
            await HotkeyListDialogs.ShowMessageAsync(
                this.XamlRoot,
                NotPossibleTitle,
                "Der Hotkey hätte danach keinen Auslöser mehr. Erfasse zuerst einen MIDI-Trigger.");
            return;
        }

        this.ViewModel.Triggers.ClearKey(hotkey);
    }

    private async void ClearMidi_Click(object sender, RoutedEventArgs e)
    {
        if (HotkeyOf(sender) is not { } hotkey)
        {
            return;
        }

        if (!ListTriggerCapture.CanClearMidi(hotkey))
        {
            await HotkeyListDialogs.ShowMessageAsync(
                this.XamlRoot,
                NotPossibleTitle,
                "Der Hotkey hätte danach keinen Auslöser mehr. Erfasse zuerst eine Tastenkombination.");
            return;
        }

        this.ViewModel.Triggers.ClearMidi(hotkey);
    }

    // The menu callbacks are async void on purpose, like event handlers: a failing dialog then
    // reaches App.UnhandledException right away instead of getting lost as an unobserved task.
    private void AssignGroup_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && HotkeyOf(sender) is { } hotkey)
        {
            HotkeyListMenus
                .AssignFolder(
                    this.ViewModel.KnownGroups,
                    folder => this.ViewModel.SetGroup(hotkey, folder),
                    async () => await this.CreateFolderAsync(hotkey))
                .ShowAt(button);
        }
    }

    private void MoveMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && HotkeyOf(sender) is { } hotkey)
        {
            HotkeyListMenus
                .MoveRow(
                    this.ViewModel.IsReorderEnabled,
                    offset => this.ViewModel.MoveBy(hotkey, offset),
                    this.ViewModel.KnownGroups,
                    folder => this.ViewModel.SetGroup(hotkey, folder),
                    async () => await this.CreateFolderAsync(hotkey))
                .ShowAt(button);
        }
    }

    private void GroupMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && GroupOf(sender) is { } group)
        {
            HotkeyListMenus
                .Folder(
                    group,
                    async () => await this.RenameFolderAsync(group),
                    async () => await this.DissolveFolderAsync(group))
                .ShowAt(button);
        }
    }

    private async void GroupName_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (GroupOf(sender) is { } group && !string.IsNullOrEmpty(group.Key))
        {
            e.Handled = true;
            await this.RenameFolderAsync(group);
        }
    }

    private async Task CreateFolderAsync(HotkeyDefinition hotkey)
    {
        if (await HotkeyListDialogs.AskNewFolderNameAsync(this.XamlRoot) is { } name)
        {
            this.ViewModel.SetGroup(hotkey, name);
        }
    }

    private async Task RenameFolderAsync(HotkeyGroup group)
    {
        if (await HotkeyListDialogs.AskFolderRenameAsync(this.XamlRoot, group.Key) is { } name)
        {
            this.ViewModel.RenameGroup(group, name);
        }
    }

    private async Task DissolveFolderAsync(HotkeyGroup group)
    {
        if (await HotkeyListDialogs.ConfirmDissolveAsync(this.XamlRoot, group))
        {
            this.ViewModel.DissolveGroup(group);
        }
    }

    private void Items_DragItemsStarting(object sender, DragItemsStartingEventArgs e) =>
        this.dragDrop.Start(GroupOf(sender), e);

    private void Group_DragOver(object sender, DragEventArgs e) => this.dragDrop.Over(e);

    private void Group_Drop(object sender, DragEventArgs e) => this.dragDrop.Drop(GroupOf(sender), e);

    private void Items_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args) =>
        this.dragDrop.Complete();
}
