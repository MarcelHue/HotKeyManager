using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;
using HotKeyManager.Services;
using Microsoft.UI.Dispatching;

namespace HotKeyManager.ViewModels;

public partial class HotkeyListViewModel : ObservableObject
{
    private readonly HotkeyManagerService _hotkeyService;
    private readonly ActionExecutor _actionExecutor;
    private readonly KeyboardHookService _keyboardHook;
    private readonly MidiService _midiService;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _isActive;

    /// <summary>Aufgeklappt-Zustand der Ordner ueber Refreshes hinweg merken.</summary>
    private readonly Dictionary<string, bool> _groupExpanded = new();

    /// <summary>Hotkey, fuer den gerade ein Ausloeser erfasst wird (null = keiner).</summary>
    private HotkeyDefinition? _capturingHotkey;

    public ObservableCollection<HotkeyGroup> Groups { get; } = new();

    /// <summary>Vorhandene Ordnernamen fuer die Zuweisung (ohne Leereintrag).</summary>
    public ObservableCollection<string> KnownGroups { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReorderEnabled))]
    private string _searchText = string.Empty;

    /// <summary>
    /// Umsortieren ist nur ohne aktive Suche moeglich — bei gefilterter Liste
    /// waere die resultierende Gesamtreihenfolge nicht nachvollziehbar.
    /// </summary>
    public bool IsReorderEnabled => string.IsNullOrWhiteSpace(SearchText);

    [ObservableProperty]
    private string _countText = "0 Hotkeys konfiguriert";

    [ObservableProperty]
    private bool _isEmptyStateVisible;

    [ObservableProperty]
    private string _emptyStateTitle = "Keine Hotkeys vorhanden";

    [ObservableProperty]
    private string _emptyStateSubtitle = "Erstelle deinen ersten Hotkey um loszulegen";

    public HotkeyListViewModel(
        HotkeyManagerService? hotkeyService = null,
        ActionExecutor? actionExecutor = null,
        DispatcherQueue? dispatcherQueue = null)
    {
        _hotkeyService = hotkeyService ?? App.Current.HotkeyManagerService;
        _actionExecutor = actionExecutor ?? App.Current.ActionExecutor;
        _keyboardHook = App.Current.KeyboardHookService;
        _midiService = App.Current.MidiService;
        // GetForCurrentThread statt App.DispatcherQueue: das VM wird waehrend der
        // MainWindow-Konstruktion erzeugt, wenn App.MainWindow noch null ist.
        _dispatcherQueue = dispatcherQueue ?? DispatcherQueue.GetForCurrentThread() ?? App.Current.DispatcherQueue;
    }

    /// <summary>Beim Betreten der Seite aufrufen (OnNavigatedTo).</summary>
    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
        _hotkeyService.HotkeysChanged += OnHotkeysChanged;
        Refresh();
    }

    /// <summary>Beim Verlassen der Seite aufrufen (OnNavigatedFrom) — verhindert Event-Handler-Leaks.</summary>
    public void Deactivate()
    {
        if (!_isActive) return;
        _isActive = false;
        _hotkeyService.HotkeysChanged -= OnHotkeysChanged;
        CancelTriggerCapture();
    }

    private void OnHotkeysChanged(object? sender, EventArgs e)
    {
        _dispatcherQueue.TryEnqueue(Refresh);
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    private void Refresh()
    {
        var all = _hotkeyService.Hotkeys;
        var filter = SearchText?.Trim() ?? string.Empty;

        IEnumerable<HotkeyDefinition> filtered = all;
        if (filter.Length > 0)
        {
            filtered = all.Where(h =>
                h.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                h.Group.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                h.TriggerDisplayText.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (h.Action?.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (h.Action?.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // Aufklapp-Zustand der aktuellen Ordner sichern
        foreach (var group in Groups)
            _groupExpanded[group.Key] = group.IsExpanded;

        Groups.Clear();
        foreach (var byGroup in filtered
                     .GroupBy(h => h.Group ?? string.Empty)
                     // "Ohne Ordner" zuletzt, sonst alphabetisch
                     .OrderBy(g => string.IsNullOrEmpty(g.Key) ? 1 : 0)
                     .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase))
        {
            var group = new HotkeyGroup(byGroup.Key)
            {
                IsExpanded = !_groupExpanded.TryGetValue(byGroup.Key, out var expanded) || expanded
            };

            foreach (var hotkey in byGroup)
                group.Items.Add(hotkey);

            Groups.Add(group);
        }

        RefreshKnownGroups(all);

        CountText = all.Count == 1 ? "1 Hotkey konfiguriert" : $"{all.Count} Hotkeys konfiguriert";

        var visibleCount = Groups.Sum(g => g.Items.Count);
        if (visibleCount == 0)
        {
            IsEmptyStateVisible = true;
            if (all.Count == 0)
            {
                EmptyStateTitle = "Keine Hotkeys vorhanden";
                EmptyStateSubtitle = "Erstelle deinen ersten Hotkey um loszulegen";
            }
            else
            {
                EmptyStateTitle = "Keine Treffer";
                EmptyStateSubtitle = $"Kein Hotkey passt zur Suche „{filter}“";
            }
        }
        else
        {
            IsEmptyStateVisible = false;
        }
    }

    private void RefreshKnownGroups(IReadOnlyList<HotkeyDefinition> all)
    {
        var names = all
            .Select(h => h.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        KnownGroups.Clear();
        foreach (var name in names)
            KnownGroups.Add(name);
    }

    /// <summary>
    /// Persistiert einen IsEnabled-Toggle. Das Model wurde bereits in-place geaendert
    /// (gleiche Instanz wie im Service), daher reicht Speichern ohne Refresh.
    /// </summary>
    public void PersistToggle() => _hotkeyService.SaveChanges();

    #region Ordner &amp; Reihenfolge

    public void SetGroup(HotkeyDefinition hotkey, string? group)
    {
        hotkey.Group = group?.Trim() ?? string.Empty;
        _hotkeyService.SaveChanges();
        Refresh();
    }

    /// <summary>
    /// Uebernimmt die aktuelle Anzeigereihenfolge (nach Drag &amp; Drop) in die
    /// gespeicherte Liste. Nur ohne aktive Suche sinnvoll.
    /// </summary>
    public void ApplyOrderFromGroups()
    {
        if (!IsReorderEnabled) return;

        var ordered = Groups.SelectMany(g => g.Items).ToList();
        _hotkeyService.SetHotkeyOrder(ordered);
    }

    /// <summary>
    /// Verschiebt einen Hotkey innerhalb seines Ordners um eine Position
    /// (Alternative zum Ziehen). Liefert false, wenn er schon am Rand steht.
    /// </summary>
    public bool MoveBy(HotkeyDefinition hotkey, int offset)
    {
        if (!IsReorderEnabled) return false;

        var group = Groups.FirstOrDefault(g => g.Items.Contains(hotkey));
        if (group == null) return false;

        var index = group.Items.IndexOf(hotkey);
        var target = index + offset;
        if (target < 0 || target >= group.Items.Count) return false;

        group.Items.Move(index, target);
        ApplyOrderFromGroups();
        return true;
    }

    /// <summary>
    /// Verschiebt einen Hotkey per Drag &amp; Drop in einen anderen Ordner
    /// (ans Ende der Zielgruppe) und persistiert Zuordnung und Reihenfolge.
    /// </summary>
    public void MoveToGroup(HotkeyDefinition hotkey, HotkeyGroup targetGroup)
    {
        var sourceGroup = Groups.FirstOrDefault(g => g.Items.Contains(hotkey));
        if (sourceGroup == targetGroup) return;

        sourceGroup?.Items.Remove(hotkey);
        targetGroup.Items.Add(hotkey);
        hotkey.Group = targetGroup.Key;

        targetGroup.IsExpanded = true;

        ApplyOrderFromGroups();

        // Leere Gruppen verschwinden, Zaehler aktualisieren
        Refresh();
    }

    #endregion

    #region Inline-Umbenennen

    public void BeginRename(HotkeyDefinition hotkey)
    {
        hotkey.EditingName = hotkey.Name;
        hotkey.IsEditingName = true;
    }

    public void CommitRename(HotkeyDefinition hotkey)
    {
        if (!hotkey.IsEditingName) return;

        var newName = hotkey.EditingName?.Trim() ?? string.Empty;
        hotkey.IsEditingName = false;

        if (newName.Length == 0 || newName == hotkey.Name) return;

        hotkey.Name = newName;
        _hotkeyService.SaveChanges();
    }

    public void CancelRename(HotkeyDefinition hotkey)
    {
        hotkey.IsEditingName = false;
    }

    #endregion

    #region Ausloeser direkt in der Liste erfassen

    public void StartKeyCapture(HotkeyDefinition hotkey)
    {
        CancelTriggerCapture();

        _capturingHotkey = hotkey;
        hotkey.CaptureHint = "Drücke eine Taste…";

        _keyboardHook.IsCapturing = true;
        _keyboardHook.KeyPressed += OnKeyPressedForCapture;
    }

    private void OnKeyPressedForCapture(object? sender, KeyEventArgs e)
    {
        e.Handled = true;

        _dispatcherQueue.TryEnqueue(() =>
        {
            var hotkey = _capturingHotkey;
            if (hotkey == null) return;

            hotkey.VirtualKeyCode = e.VirtualKeyCode;
            hotkey.Modifiers = e.Modifiers;

            CancelTriggerCapture();
            _hotkeyService.SaveChanges();
        });
    }

    public void StartMidiLearn(HotkeyDefinition hotkey)
    {
        CancelTriggerCapture();

        _capturingHotkey = hotkey;
        hotkey.CaptureHint = "Drücke ein Pad am MIDI-Gerät…";

        _midiService.IsLearning = true;
        _midiService.MidiReceived += OnMidiReceivedForCapture;
    }

    private void OnMidiReceivedForCapture(object? sender, MidiTriggerEventArgs e)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            var hotkey = _capturingHotkey;
            if (hotkey == null) return;

            hotkey.MidiTrigger = new MidiTrigger
            {
                Kind = e.Kind,
                Channel = e.Channel,
                Number = e.Number
            };

            CancelTriggerCapture();
            _hotkeyService.SaveChanges();
        });
    }

    public void ClearKeyTrigger(HotkeyDefinition hotkey)
    {
        CancelTriggerCapture();

        // Ohne jeden Ausloeser waere der Hotkey nicht mehr ausloesbar
        if (hotkey.MidiTrigger == null) return;

        hotkey.VirtualKeyCode = 0;
        hotkey.Modifiers = ModifierKeys.None;
        _hotkeyService.SaveChanges();
    }

    public void ClearMidiTrigger(HotkeyDefinition hotkey)
    {
        CancelTriggerCapture();

        if (hotkey.VirtualKeyCode == 0) return;

        hotkey.MidiTrigger = null;
        _hotkeyService.SaveChanges();
    }

    /// <summary>Bricht eine laufende Erfassung ab und gibt Hook bzw. MIDI wieder frei.</summary>
    public void CancelTriggerCapture()
    {
        if (_capturingHotkey == null) return;

        _keyboardHook.KeyPressed -= OnKeyPressedForCapture;
        _keyboardHook.IsCapturing = false;

        _midiService.MidiReceived -= OnMidiReceivedForCapture;
        _midiService.IsLearning = false;

        _capturingHotkey.CaptureHint = null;
        _capturingHotkey = null;
    }

    #endregion

    [RelayCommand]
    private async Task RunAsync(HotkeyDefinition? hotkey)
    {
        if (hotkey?.Action == null) return;

        // Gleiche Ausfuehrung wie beim echten Hotkey-Trigger (inkl. Kernel-Injection und Targeting)
        await _actionExecutor.ExecuteAsync(
            hotkey.Action,
            hotkey.WindowMode,
            hotkey.TargetProcessName,
            hotkey.TargetWindowTitle);
    }

    public void Delete(HotkeyDefinition hotkey) => _hotkeyService.RemoveHotkey(hotkey.Id);
}
