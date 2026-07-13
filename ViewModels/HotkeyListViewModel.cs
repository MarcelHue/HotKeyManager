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
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _isActive;

    public ObservableCollection<HotkeyDefinition> Hotkeys { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

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
                h.KeyDisplayText.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                (h.Action?.DisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (h.Action?.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Hotkeys.Clear();
        foreach (var hotkey in filtered)
            Hotkeys.Add(hotkey);

        CountText = all.Count == 1 ? "1 Hotkey konfiguriert" : $"{all.Count} Hotkeys konfiguriert";

        if (Hotkeys.Count == 0)
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

    /// <summary>
    /// Persistiert einen IsEnabled-Toggle. Das Model wurde bereits in-place geaendert
    /// (gleiche Instanz wie im Service), daher reicht Speichern ohne Refresh.
    /// </summary>
    public void PersistToggle() => _hotkeyService.SaveChanges();

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
