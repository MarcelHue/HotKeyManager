using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

/// <summary>
/// ViewModel of the hotkey list: search, folders, order and running a hotkey. Trigger capture and
/// inline rename inside the list live in <see cref="Triggers"/> and <see cref="Rename"/>.
/// </summary>
public partial class HotkeyListViewModel : ObservableObject
{
    private const string NoHotkeysTitle = "Keine Hotkeys vorhanden";
    private const string NoHotkeysSubtitle = "Erstelle deinen ersten Hotkey um loszulegen";

    private readonly HotkeyManagerService hotkeyService;
    private readonly ActionExecutor actionExecutor;
    private readonly DispatcherQueue dispatcherQueue;

    /// <summary>Folder expand state, kept across refreshes.</summary>
    private readonly Dictionary<string, bool> groupExpanded = new();
    private bool isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReorderEnabled))]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string countText = "0 Hotkeys konfiguriert";

    [ObservableProperty]
    private bool isEmptyStateVisible;

    [ObservableProperty]
    private string emptyStateTitle = NoHotkeysTitle;

    [ObservableProperty]
    private string emptyStateSubtitle = NoHotkeysSubtitle;

    /// <summary>Creates the list; services that are not passed in default to the app-wide instances.</summary>
    public HotkeyListViewModel(
        HotkeyManagerService? hotkeyService = null,
        ActionExecutor? actionExecutor = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        this.hotkeyService = hotkeyService ?? App.Current.HotkeyManagerService;
        this.actionExecutor = actionExecutor ?? App.Current.ActionExecutor;

        // GetForCurrentThread first: the ViewModel is created while MainWindow is being constructed,
        // when App.MainWindow (and with it App.DispatcherQueue) is still null.
        var queue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread() ?? App.Current.DispatcherQueue;
        this.Triggers = new ListTriggerCapture(
            App.Current.KeyboardHookService,
            App.Current.MidiService,
            queue,
            this.hotkeyService.SaveChanges);
        this.dispatcherQueue = queue;
        this.Rename = new InlineRename(this.hotkeyService.SaveChanges);
    }

    public ObservableCollection<HotkeyGroup> Groups { get; } = new();

    /// <summary>Existing folder names for assigning a folder (without the empty entry).</summary>
    public ObservableCollection<string> KnownGroups { get; } = new();

    public ListTriggerCapture Triggers { get; }

    public InlineRename Rename { get; }

    /// <summary>
    /// Reordering only works without an active search; in a filtered list the resulting overall
    /// order would be impossible to follow.
    /// </summary>
    public bool IsReorderEnabled => string.IsNullOrWhiteSpace(this.SearchText);

    /// <summary>Call when the page is shown (OnNavigatedTo).</summary>
    public void Activate()
    {
        if (this.isActive)
        {
            return;
        }

        this.isActive = true;
        this.hotkeyService.HotkeysChanged += this.OnHotkeysChanged;
        this.Refresh();
    }

    /// <summary>Call when the page is left (OnNavigatedFrom); prevents event handler leaks.</summary>
    public void Deactivate()
    {
        if (!this.isActive)
        {
            return;
        }

        this.isActive = false;
        this.hotkeyService.HotkeysChanged -= this.OnHotkeysChanged;
        this.Triggers.Cancel();
    }

    /// <summary>
    /// Saves an enabled toggle. The model was already changed in place (same instance as in the
    /// service), so saving without a refresh is enough.
    /// </summary>
    public void PersistToggle() => this.hotkeyService.SaveChanges();

    /// <summary>Moves a hotkey into a folder (empty name = no folder).</summary>
    public void SetGroup(HotkeyDefinition hotkey, string? group)
    {
        hotkey.Group = group?.Trim() ?? string.Empty;
        this.SaveAndRefresh();
    }

    /// <summary>Saves the displayed order (after drag and drop); only meaningful without a search.</summary>
    public void ApplyOrderFromGroups()
    {
        if (this.IsReorderEnabled)
        {
            this.hotkeyService.SetHotkeyOrder(this.Groups.SelectMany(g => g.Items).ToList());
        }
    }

    /// <summary>Renames a folder for all its hotkeys; an existing target name merges both folders.</summary>
    public void RenameGroup(HotkeyGroup group, string? newName)
    {
        var name = newName?.Trim() ?? string.Empty;
        if (name.Length == 0 || name == group.Key)
        {
            return;
        }

        this.ReassignGroup(group.Key, name);
        this.groupExpanded[name] = group.IsExpanded;
        this.groupExpanded.Remove(group.Key);
        this.SaveAndRefresh();
    }

    /// <summary>Dissolves a folder; its hotkeys stay and move to "Ohne Ordner".</summary>
    public void DissolveGroup(HotkeyGroup group)
    {
        if (string.IsNullOrEmpty(group.Key))
        {
            return;
        }

        this.ReassignGroup(group.Key, string.Empty);
        this.groupExpanded.Remove(group.Key);
        this.SaveAndRefresh();
    }

    /// <summary>
    /// Moves a hotkey one position within its folder (alternative to dragging).
    /// Returns false when it is already at the edge.
    /// </summary>
    public bool MoveBy(HotkeyDefinition hotkey, int offset)
    {
        var group = this.Groups.FirstOrDefault(g => g.Items.Contains(hotkey));
        if (!this.IsReorderEnabled || group is null)
        {
            return false;
        }

        var index = group.Items.IndexOf(hotkey);
        var target = index + offset;
        if (target < 0 || target >= group.Items.Count)
        {
            return false;
        }

        group.Items.Move(index, target);
        this.ApplyOrderFromGroups();
        return true;
    }

    /// <summary>Moves a dropped hotkey to the end of another folder and saves folder and order.</summary>
    public void MoveToGroup(HotkeyDefinition hotkey, HotkeyGroup targetGroup)
    {
        var sourceGroup = this.Groups.FirstOrDefault(g => g.Items.Contains(hotkey));
        if (sourceGroup == targetGroup)
        {
            return;
        }

        sourceGroup?.Items.Remove(hotkey);
        targetGroup.Items.Add(hotkey);
        hotkey.Group = targetGroup.Key;
        targetGroup.IsExpanded = true;
        this.ApplyOrderFromGroups();

        // Rebuild so an emptied folder disappears and the counters update.
        this.Refresh();
    }

    /// <summary>Deletes a hotkey.</summary>
    public void Delete(HotkeyDefinition hotkey) => this.hotkeyService.RemoveHotkey(hotkey.Id);

    [RelayCommand]
    private async Task RunAsync(HotkeyDefinition? hotkey)
    {
        if (hotkey?.Action is null)
        {
            return;
        }

        // Same execution as a real trigger, including kernel injection and window targeting.
        await this.actionExecutor.ExecuteAsync(
            hotkey.Action,
            hotkey.WindowMode,
            hotkey.TargetProcessName,
            hotkey.TargetWindowTitle);
    }

    private void OnHotkeysChanged(object? sender, EventArgs e) => this.dispatcherQueue.TryEnqueue(this.Refresh);

    partial void OnSearchTextChanged(string value) => this.Refresh();

    private void ReassignGroup(string from, string to)
    {
        foreach (var hotkey in this.hotkeyService.Hotkeys.Where(h => h.Group == from).ToList())
        {
            hotkey.Group = to;
        }
    }

    private void SaveAndRefresh()
    {
        this.hotkeyService.SaveChanges();
        this.Refresh();
    }

    private void Refresh()
    {
        var all = this.hotkeyService.Hotkeys;
        var filter = this.SearchText?.Trim() ?? string.Empty;

        foreach (var group in this.Groups)
        {
            this.groupExpanded[group.Key] = group.IsExpanded;
        }

        this.Groups.Clear();
        foreach (var folder in HotkeyGrouping.ByFolder(HotkeyGrouping.Filter(all, filter)))
        {
            var group = new HotkeyGroup(folder.Key)
            {
                IsExpanded = !this.groupExpanded.TryGetValue(folder.Key, out var expanded) || expanded,
            };
            foreach (var hotkey in folder)
            {
                group.Items.Add(hotkey);
            }

            this.Groups.Add(group);
        }

        this.KnownGroups.Clear();
        foreach (var name in HotkeyGrouping.FolderNames(all))
        {
            this.KnownGroups.Add(name);
        }

        this.UpdateCounters(all.Count, filter);
    }

    private void UpdateCounters(int total, string filter)
    {
        this.CountText = total == 1 ? "1 Hotkey konfiguriert" : $"{total} Hotkeys konfiguriert";
        this.IsEmptyStateVisible = this.Groups.Sum(g => g.Items.Count) == 0;
        if (!this.IsEmptyStateVisible)
        {
            return;
        }

        if (total == 0)
        {
            this.EmptyStateTitle = NoHotkeysTitle;
            this.EmptyStateSubtitle = NoHotkeysSubtitle;
        }
        else
        {
            this.EmptyStateTitle = "Keine Treffer";
            this.EmptyStateSubtitle = $"Kein Hotkey passt zur Suche „{filter}“";
        }
    }
}
