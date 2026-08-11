using System.ComponentModel;
using System.Runtime.CompilerServices;
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

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Meldet, dass sich die Ausloeser-Anzeige geaendert hat.</summary>
    private void NotifyTriggerDisplay()
    {
        Notify(nameof(KeyDisplayText));
        Notify(nameof(TriggerDisplayText));
    }

    public Guid Id { get; set; } = Guid.NewGuid();

    private string _name = string.Empty;
    public string Name
    {
        get => _name;
        set
        {
            if (_name == value) return;
            _name = value;
            Notify();
        }
    }

    /// <summary>Ordner/Gruppe fuer die Listenansicht. Leer = "Ohne Ordner".</summary>
    private string _group = string.Empty;
    public string Group
    {
        get => _group;
        set
        {
            if (_group == value) return;
            _group = value;
            Notify();
        }
    }

    private int _virtualKeyCode;
    public int VirtualKeyCode
    {
        get => _virtualKeyCode;
        set
        {
            if (_virtualKeyCode == value) return;
            _virtualKeyCode = value;
            NotifyTriggerDisplay();
        }
    }

    private ModifierKeys _modifiers = ModifierKeys.None;
    public ModifierKeys Modifiers
    {
        get => _modifiers;
        set
        {
            if (_modifiers == value) return;
            _modifiers = value;
            NotifyTriggerDisplay();
        }
    }

    private bool _isEnabled = true;
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value) return;
            _isEnabled = value;
            Notify();
        }
    }

    public ActionBase? Action { get; set; }

    /// <summary>Optionaler MIDI-Ausloeser (z.B. Rodecaster-Pad). Null = nur Tastatur.</summary>
    private MidiTrigger? _midiTrigger;
    public MidiTrigger? MidiTrigger
    {
        get => _midiTrigger;
        set
        {
            _midiTrigger = value;
            NotifyTriggerDisplay();
        }
    }

    /// <summary>Fenster-Targeting Modus.</summary>
    public WindowTargetMode WindowMode { get; set; } = WindowTargetMode.None;
    /// <summary>Zielprozess-Name (z.B. "notepad", "chrome"). Null = beliebig.</summary>
    public string? TargetProcessName { get; set; }
    /// <summary>Zielfenster-Titel mit Wildcard-Support (z.B. "*Editor*"). Null = beliebig.</summary>
    public string? TargetWindowTitle { get; set; }

    #region UI-Zustand (nicht persistiert)

    // Bewusst am Model statt in einem eigenen Zeilen-ViewModel: die Liste bindet
    // direkt auf HotkeyDefinition, und diese Zustaende gelten immer genau einer Zeile.

    private bool _isEditingName;
    [JsonIgnore]
    public bool IsEditingName
    {
        get => _isEditingName;
        set
        {
            if (_isEditingName == value) return;
            _isEditingName = value;
            Notify();
            Notify(nameof(IsNotEditingName));
        }
    }

    [JsonIgnore]
    public bool IsNotEditingName => !_isEditingName;

    /// <summary>Puffer fuer das Inline-Umbenennen (wird erst bei Enter uebernommen).</summary>
    private string _editingName = string.Empty;
    [JsonIgnore]
    public string EditingName
    {
        get => _editingName;
        set
        {
            if (_editingName == value) return;
            _editingName = value;
            Notify();
        }
    }

    /// <summary>Text, der waehrend einer laufenden Erfassung im Ausloeser-Feld steht.</summary>
    private string? _captureHint;
    [JsonIgnore]
    public string? CaptureHint
    {
        get => _captureHint;
        set
        {
            if (_captureHint == value) return;
            _captureHint = value;
            Notify();
            Notify(nameof(TriggerDisplayText));
        }
    }

    #endregion

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
            if (!string.IsNullOrEmpty(CaptureHint))
                return CaptureHint;

            var parts = new List<string>();
            if (VirtualKeyCode != 0)
                parts.Add(KeyDisplayText);
            if (MidiTrigger != null)
                parts.Add($"MIDI: {MidiTrigger.DisplayText}");
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "Kein Auslöser";
        }
    }
}
