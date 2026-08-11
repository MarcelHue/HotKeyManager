using HotKeyManager.Helpers;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

public class HotkeyManagerService
{
    private readonly KeyboardHookService _keyboardHook;
    private readonly ActionExecutor _actionExecutor;
    private readonly ConfigurationService _configService;
    private readonly MidiService? _midiService;
    private readonly List<HotkeyDefinition> _hotkeys = new();

    public IReadOnlyList<HotkeyDefinition> Hotkeys => _hotkeys.AsReadOnly();

    public event EventHandler? HotkeysChanged;

    public HotkeyManagerService(
        KeyboardHookService keyboardHook,
        ActionExecutor actionExecutor,
        ConfigurationService configService,
        MidiService? midiService = null)
    {
        _keyboardHook = keyboardHook;
        _actionExecutor = actionExecutor;
        _configService = configService;
        _midiService = midiService;

        _keyboardHook.KeyPressed += OnKeyPressed;

        if (_midiService != null)
            _midiService.MidiReceived += OnMidiReceived;
    }

    private void OnMidiReceived(object? sender, MidiTriggerEventArgs e)
    {
        // Waehrend des Einlernens im Editor keine Hotkeys ausloesen
        if (_midiService!.IsLearning) return;

        var matchingHotkey = _hotkeys.FirstOrDefault(h =>
            h.IsEnabled &&
            h.MidiTrigger != null &&
            h.MidiTrigger.Matches(e.Kind, e.Channel, e.Number));

        if (matchingHotkey?.Action != null)
        {
            if (!ShouldExecuteHotkey(matchingHotkey))
                return;

            App.Current.LogService.Debug($"MIDI-Trigger matched: {matchingHotkey.Name} ({e.Kind} {e.Number}, Kanal {e.Channel + 1}, Gerät: {e.DeviceName})");
            _ = ExecuteHotkeyAsync(matchingHotkey);
        }
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        if (_keyboardHook.IsCapturing) return;

        var matchingHotkey = _hotkeys.FirstOrDefault(h =>
            h.IsEnabled &&
            h.VirtualKeyCode == e.VirtualKeyCode &&
            h.Modifiers == e.Modifiers);

        if (matchingHotkey?.Action != null)
        {
            if (!ShouldExecuteHotkey(matchingHotkey))
                return;

            App.Current.LogService.Debug($"Hotkey matched: {matchingHotkey.Name} (VK={e.VirtualKeyCode}, Modifiers={e.Modifiers})");
            e.Handled = true;
            _ = ExecuteHotkeyAsync(matchingHotkey);
        }
    }

    private bool ShouldExecuteHotkey(HotkeyDefinition hotkey)
    {
        if (hotkey.WindowMode == WindowTargetMode.OnlyWhenActive)
            return WindowHelper.IsActiveWindowMatch(hotkey.TargetProcessName, hotkey.TargetWindowTitle);
        return true;
    }

    private async Task ExecuteHotkeyAsync(HotkeyDefinition hotkey)
    {
        try
        {
            await _actionExecutor.ExecuteAsync(
                hotkey.Action!,
                hotkey.WindowMode,
                hotkey.TargetProcessName,
                hotkey.TargetWindowTitle);
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error($"Fehler beim Ausfuehren der Hotkey-Aktion '{hotkey.Name}'", ex);
        }
    }

    public void LoadHotkeys(IEnumerable<HotkeyDefinition> hotkeys)
    {
        _hotkeys.Clear();
        _hotkeys.AddRange(hotkeys);
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddHotkey(HotkeyDefinition hotkey)
    {
        _hotkeys.Add(hotkey);
        SaveAndNotify();
    }

    public void UpdateHotkey(HotkeyDefinition hotkey)
    {
        var index = _hotkeys.FindIndex(h => h.Id == hotkey.Id);
        if (index >= 0)
        {
            _hotkeys[index] = hotkey;
            SaveAndNotify();
        }
    }

    public void RemoveHotkey(Guid id)
    {
        var hotkey = _hotkeys.FirstOrDefault(h => h.Id == id);
        if (hotkey != null)
        {
            _hotkeys.Remove(hotkey);
            SaveAndNotify();
        }
    }

    public void ToggleHotkey(Guid id)
    {
        var hotkey = _hotkeys.FirstOrDefault(h => h.Id == id);
        if (hotkey != null)
        {
            hotkey.IsEnabled = !hotkey.IsEnabled;
            _configService.Configuration.Hotkeys = _hotkeys.ToList();
            _ = _configService.SaveAsync();
            // Kein HotkeysChanged - UI aktualisiert sich via INotifyPropertyChanged-Binding
        }
    }

    /// <summary>
    /// Setzt die Reihenfolge der Hotkeys neu (Drag &amp; Drop in der Liste) und speichert.
    /// Es wird kein HotkeysChanged ausgeloest — die UI zeigt die neue Reihenfolge bereits.
    /// Die uebergebene Menge muss dieselben Eintraege enthalten wie die aktuelle Liste.
    /// </summary>
    public void SetHotkeyOrder(IEnumerable<HotkeyDefinition> ordered)
    {
        var list = ordered.ToList();

        if (list.Count != _hotkeys.Count || list.Distinct().Count() != _hotkeys.Count
            || !list.All(_hotkeys.Contains))
        {
            App.Current.LogService.Warning("Neue Hotkey-Reihenfolge passt nicht zur aktuellen Liste — ignoriert.");
            return;
        }

        _hotkeys.Clear();
        _hotkeys.AddRange(list);
        SaveChanges();
    }

    /// <summary>
    /// Persistiert den aktuellen Zustand ohne HotkeysChanged auszuloesen.
    /// Fuer In-Place-Aenderungen, die die UI bereits per Binding anzeigt (z.B. IsEnabled-Toggle).
    /// </summary>
    public void SaveChanges()
    {
        _configService.Configuration.Hotkeys = _hotkeys.ToList();
        _ = _configService.SaveAsync();
    }

    private void SaveAndNotify()
    {
        _configService.Configuration.Hotkeys = _hotkeys.ToList();
        _ = _configService.SaveAsync();
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }
}
