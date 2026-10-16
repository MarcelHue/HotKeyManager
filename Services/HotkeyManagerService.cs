using HotKeyManager.Helpers;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

/// <summary>Matches keyboard, MIDI and DisplayPad input against the hotkeys and runs their actions.</summary>
public class HotkeyManagerService
{
    private readonly KeyboardHookService keyboardHook;
    private readonly ActionExecutor actionExecutor;
    private readonly ConfigurationService configService;
    private readonly MidiService? midiService;
    private readonly PadDeckBridgeService? padBridge;
    private readonly List<HotkeyDefinition> hotkeys = new();

    /// <summary>Wires up all input sources; MIDI and PadDeck are optional.</summary>
    public HotkeyManagerService(
        KeyboardHookService keyboardHook,
        ActionExecutor actionExecutor,
        ConfigurationService configService,
        MidiService? midiService = null,
        PadDeckBridgeService? padBridge = null)
    {
        this.keyboardHook = keyboardHook;
        this.actionExecutor = actionExecutor;
        this.configService = configService;
        this.midiService = midiService;
        this.padBridge = padBridge;

        this.keyboardHook.KeyPressed += this.OnKeyPressed;

        if (this.midiService is not null)
        {
            this.midiService.MidiReceived += this.OnMidiReceived;
        }

        if (this.padBridge is not null)
        {
            this.padBridge.PadKey += this.OnPadKey;
            this.padBridge.ClaimsProvider = this.BuildPadClaims;
            this.HotkeysChanged += (_, _) => _ = this.padBridge.PushClaimsAsync();
        }
    }

    /// <summary>Raised after hotkeys were loaded, added, updated or removed.</summary>
    public event EventHandler? HotkeysChanged;

    /// <summary>All hotkeys in list order.</summary>
    public IReadOnlyList<HotkeyDefinition> Hotkeys => this.hotkeys.AsReadOnly();

    /// <summary>Replaces all hotkeys (on startup and after an import).</summary>
    public void LoadHotkeys(IEnumerable<HotkeyDefinition> hotkeys)
    {
        this.hotkeys.Clear();
        this.hotkeys.AddRange(hotkeys);
        this.HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds a hotkey and saves.</summary>
    public void AddHotkey(HotkeyDefinition hotkey)
    {
        this.hotkeys.Add(hotkey);
        this.SaveAndNotify();
    }

    /// <summary>Replaces the hotkey with the same id and saves.</summary>
    public void UpdateHotkey(HotkeyDefinition hotkey)
    {
        var index = this.hotkeys.FindIndex(h => h.Id == hotkey.Id);
        if (index >= 0)
        {
            this.hotkeys[index] = hotkey;
            this.SaveAndNotify();
        }
    }

    /// <summary>Removes a hotkey and saves.</summary>
    public void RemoveHotkey(Guid id)
    {
        var hotkey = this.hotkeys.FirstOrDefault(h => h.Id == id);
        if (hotkey is not null)
        {
            this.hotkeys.Remove(hotkey);
            this.SaveAndNotify();
        }
    }

    /// <summary>Switches a hotkey on or off and saves.</summary>
    public void ToggleHotkey(Guid id)
    {
        var hotkey = this.hotkeys.FirstOrDefault(h => h.Id == id);
        if (hotkey is not null)
        {
            hotkey.IsEnabled = !hotkey.IsEnabled;
            this.configService.Configuration.Hotkeys = this.hotkeys.ToList();
            _ = this.configService.SaveAsync();

            // No HotkeysChanged: the list updates itself through INotifyPropertyChanged.
        }
    }

    /// <summary>
    /// Sets a new hotkey order (drag and drop in the list) and saves, without HotkeysChanged
    /// because the list already shows the new order. The given set must match the current list.
    /// </summary>
    public void SetHotkeyOrder(IEnumerable<HotkeyDefinition> ordered)
    {
        var list = ordered.ToList();
        var isSameSet = list.Count == this.hotkeys.Count
            && list.Distinct().Count() == this.hotkeys.Count
            && list.All(this.hotkeys.Contains);
        if (!isSameSet)
        {
            App.Current.LogService.Warning("Neue Hotkey-Reihenfolge passt nicht zur aktuellen Liste — ignoriert.");
            return;
        }

        this.hotkeys.Clear();
        this.hotkeys.AddRange(list);
        this.SaveChanges();
    }

    /// <summary>
    /// Saves the current state without HotkeysChanged, for in-place edits the UI already shows
    /// through bindings (e.g. the enabled toggle).
    /// </summary>
    public void SaveChanges()
    {
        this.configService.Configuration.Hotkeys = this.hotkeys.ToList();
        _ = this.configService.SaveAsync();

        // An inline rename changes the claim label PadDeck shows on the greyed-out slot.
        if (this.padBridge is not null)
        {
            _ = this.padBridge.PushClaimsAsync();
        }
    }

    private void OnPadKey(object? sender, PadKeyEventArgs e)
    {
        // PadDeck only reports keys we claimed; a hotkey fires on press.
        if (!e.Pressed)
        {
            return;
        }

        var matchingHotkey = this.hotkeys.FirstOrDefault(h =>
            h.IsEnabled && h.PadTrigger is not null && h.PadTrigger.Matches(e.Key.ProfileId, e.Key.PageId, e.Key.Slot));
        if (matchingHotkey?.Action is null || !this.ShouldExecuteHotkey(matchingHotkey))
        {
            return;
        }

        App.Current.LogService.Debug(
            $"Pad-Trigger matched: {matchingHotkey.Name} (Taste {e.Key.Slot + 1}, {e.Key.PageName})");
        _ = this.ExecuteHotkeyAsync(matchingHotkey);
    }

    /// <summary>All pad triggers as claims for PadDeck, which greys out these keys.</summary>
    private IReadOnlyList<PadClaim> BuildPadClaims() => this.hotkeys
        .Where(h => h.PadTrigger is not null)
        .Select(h => new PadClaim
        {
            ProfileId = h.PadTrigger!.ProfileId,
            PageId = h.PadTrigger.PageId,
            Slot = h.PadTrigger.Slot,
            Label = h.Name,
            ExternalId = h.Id.ToString(),
        })
        .ToList();

    private void OnMidiReceived(object? sender, MidiTriggerEventArgs e)
    {
        // While the editor is learning a MIDI trigger, no hotkey may fire.
        if (this.midiService!.IsLearning)
        {
            return;
        }

        var matchingHotkey = this.hotkeys.FirstOrDefault(h =>
            h.IsEnabled && h.MidiTrigger is not null && h.MidiTrigger.Matches(e.Kind, e.Channel, e.Number));
        if (matchingHotkey?.Action is null || !this.ShouldExecuteHotkey(matchingHotkey))
        {
            return;
        }

        var source = $"{e.Kind} {e.Number}, Kanal {e.Channel + 1}, Gerät: {e.DeviceName}";
        App.Current.LogService.Debug($"MIDI-Trigger matched: {matchingHotkey.Name} ({source})");
        _ = this.ExecuteHotkeyAsync(matchingHotkey);
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        if (this.keyboardHook.IsCapturing)
        {
            return;
        }

        var matchingHotkey = this.hotkeys.FirstOrDefault(h =>
            h.IsEnabled && h.VirtualKeyCode == e.VirtualKeyCode && h.Modifiers == e.Modifiers);
        if (matchingHotkey?.Action is null || !this.ShouldExecuteHotkey(matchingHotkey))
        {
            return;
        }

        App.Current.LogService.Debug(
            $"Hotkey matched: {matchingHotkey.Name} (VK={e.VirtualKeyCode}, Modifiers={e.Modifiers})");
        e.Handled = true;
        _ = this.ExecuteHotkeyAsync(matchingHotkey);
    }

    private bool ShouldExecuteHotkey(HotkeyDefinition hotkey)
    {
        if (hotkey.WindowMode == WindowTargetMode.OnlyWhenActive)
        {
            return WindowHelper.IsActiveWindowMatch(hotkey.TargetProcessName, hotkey.TargetWindowTitle);
        }

        return true;
    }

    private async Task ExecuteHotkeyAsync(HotkeyDefinition hotkey)
    {
        try
        {
            await this.actionExecutor.ExecuteAsync(
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

    private void SaveAndNotify()
    {
        this.configService.Configuration.Hotkeys = this.hotkeys.ToList();
        _ = this.configService.SaveAsync();
        this.HotkeysChanged?.Invoke(this, EventArgs.Empty);
    }
}
