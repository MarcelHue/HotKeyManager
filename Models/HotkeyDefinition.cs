using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>A hotkey: its triggers (key, MIDI, DisplayPad), its action and its list-row state.</summary>
public class HotkeyDefinition : INotifyPropertyChanged
{
    private string name = string.Empty;
    private string group = string.Empty;
    private int virtualKeyCode;
    private ModifierKeys modifiers = ModifierKeys.None;
    private bool isEnabled = true;
    private MidiTrigger? midiTrigger;
    private PadTrigger? padTrigger;
    private bool isEditingName;
    private string editingName = string.Empty;
    private string? captureHint;

    /// <summary>Raised when a bound property changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => this.name;
        set
        {
            if (this.name == value)
            {
                return;
            }

            this.name = value;
            this.Notify();
        }
    }

    /// <summary>Folder in the list view; empty means "Ohne Ordner".</summary>
    public string Group
    {
        get => this.group;
        set
        {
            if (this.group == value)
            {
                return;
            }

            this.group = value;
            this.Notify();
        }
    }

    public int VirtualKeyCode
    {
        get => this.virtualKeyCode;
        set
        {
            if (this.virtualKeyCode == value)
            {
                return;
            }

            this.virtualKeyCode = value;
            this.NotifyTriggerDisplay();
        }
    }

    public ModifierKeys Modifiers
    {
        get => this.modifiers;
        set
        {
            if (this.modifiers == value)
            {
                return;
            }

            this.modifiers = value;
            this.NotifyTriggerDisplay();
        }
    }

    public bool IsEnabled
    {
        get => this.isEnabled;
        set
        {
            if (this.isEnabled == value)
            {
                return;
            }

            this.isEnabled = value;
            this.Notify();
        }
    }

    public ActionBase? Action { get; set; }

    /// <summary>Optional MIDI trigger (e.g. a Rodecaster pad); null means keyboard only.</summary>
    public MidiTrigger? MidiTrigger
    {
        get => this.midiTrigger;
        set
        {
            this.midiTrigger = value;
            this.NotifyTriggerDisplay();
        }
    }

    /// <summary>Optional DisplayPad trigger via PadDeck; null means no pad key.</summary>
    public PadTrigger? PadTrigger
    {
        get => this.padTrigger;
        set
        {
            this.padTrigger = value;
            this.NotifyTriggerDisplay();
        }
    }

    public WindowTargetMode WindowMode { get; set; } = WindowTargetMode.None;

    /// <summary>Target process name (e.g. "notepad", "chrome"); null means any.</summary>
    public string? TargetProcessName { get; set; }

    /// <summary>Target window title with wildcards (e.g. "*Editor*"); null means any.</summary>
    public string? TargetWindowTitle { get; set; }

    // The list-row state below lives on the model on purpose: the list binds directly to
    // HotkeyDefinition, and each of these states always belongs to exactly one row. Not persisted.

    [JsonIgnore]
    public bool IsEditingName
    {
        get => this.isEditingName;
        set
        {
            if (this.isEditingName == value)
            {
                return;
            }

            this.isEditingName = value;
            this.Notify();
            this.Notify(nameof(this.IsNotEditingName));
        }
    }

    [JsonIgnore]
    public bool IsNotEditingName => !this.isEditingName;

    /// <summary>Buffer for inline renaming; applied only on Enter.</summary>
    [JsonIgnore]
    public string EditingName
    {
        get => this.editingName;
        set
        {
            if (this.editingName == value)
            {
                return;
            }

            this.editingName = value;
            this.Notify();
        }
    }

    /// <summary>Text shown in the trigger field while a capture is running.</summary>
    [JsonIgnore]
    public string? CaptureHint
    {
        get => this.captureHint;
        set
        {
            if (this.captureHint == value)
            {
                return;
            }

            this.captureHint = value;
            this.Notify();
            this.Notify(nameof(this.TriggerDisplayText));
        }
    }

    [JsonIgnore]
    public string KeyDisplayText => this.VirtualKeyCode == 0
        ? string.Empty
        : Helpers.KeyHelper.FormatCombination(this.VirtualKeyCode, this.Modifiers);

    /// <summary>All triggers (key, MIDI, pad) combined for the list.</summary>
    [JsonIgnore]
    public string TriggerDisplayText
    {
        get
        {
            if (!string.IsNullOrEmpty(this.CaptureHint))
            {
                return this.CaptureHint;
            }

            var parts = new List<string>();
            if (this.VirtualKeyCode != 0)
            {
                parts.Add(this.KeyDisplayText);
            }

            if (this.MidiTrigger is not null)
            {
                parts.Add($"MIDI: {this.MidiTrigger.DisplayText}");
            }

            if (this.PadTrigger is not null)
            {
                parts.Add($"Pad: {this.PadTrigger.DisplayText}");
            }

            return parts.Count > 0 ? string.Join("  ·  ", parts) : "Kein Auslöser";
        }
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>Tells the list that the combined trigger text changed.</summary>
    private void NotifyTriggerDisplay()
    {
        this.Notify(nameof(this.KeyDisplayText));
        this.Notify(nameof(this.TriggerDisplayText));
    }
}
