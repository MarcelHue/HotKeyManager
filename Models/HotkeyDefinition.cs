using System.ComponentModel;
using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>
/// Modus fuer Fenster-Targeting bei Hotkeys.
/// </summary>
public enum WindowTargetMode
{
    /// <summary>Keine Fensterbeschraenkung - Hotkey funktioniert immer.</summary>
    None,
    /// <summary>Hotkey nur ausloesen wenn das Zielfenster aktiv ist.</summary>
    OnlyWhenActive,
    /// <summary>Tasten an Hintergrund-Fenster senden (auch wenn nicht aktiv).</summary>
    SendToBackground
}

public class HotkeyDefinition : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int VirtualKeyCode { get; set; }
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }
    }
    public ActionBase? Action { get; set; }

    /// <summary>Optionaler MIDI-Ausloeser (z.B. Rodecaster-Pad). Null = nur Tastatur.</summary>
    public MidiTrigger? MidiTrigger { get; set; }
    
    /// <summary>Fenster-Targeting Modus.</summary>
    public WindowTargetMode WindowMode { get; set; } = WindowTargetMode.None;
    /// <summary>Zielprozess-Name (z.B. "notepad", "chrome"). Null = beliebig.</summary>
    public string? TargetProcessName { get; set; }
    /// <summary>Zielfenster-Titel mit Wildcard-Support (z.B. "*Editor*"). Null = beliebig.</summary>
    public string? TargetWindowTitle { get; set; }
    
    [JsonIgnore]
    public string KeyDisplayText
    {
        get
        {
            if (VirtualKeyCode == 0)
                return string.Empty;

            var parts = new List<string>();

            if (Modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");

            parts.Add(Helpers.KeyHelper.GetKeyName(VirtualKeyCode));

            return string.Join(" + ", parts);
        }
    }

    /// <summary>Kombinierte Anzeige aller Ausloeser (Taste und/oder MIDI) fuer die Liste.</summary>
    [JsonIgnore]
    public string TriggerDisplayText
    {
        get
        {
            var parts = new List<string>();
            if (VirtualKeyCode != 0)
                parts.Add(KeyDisplayText);
            if (MidiTrigger != null)
                parts.Add($"MIDI: {MidiTrigger.DisplayText}");
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "Kein Auslöser";
        }
    }
}
