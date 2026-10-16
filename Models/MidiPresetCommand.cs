using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>A single documented MIDI command of a device.</summary>
public class MidiPresetCommand
{
    private const string NoteOnKind = "NoteOn";
    private const int MaxMidiValue = 127;

    public string Name { get; set; } = string.Empty;

    /// <summary>"NoteOn" or "ControlChange".</summary>
    public string Kind { get; set; } = "ControlChange";

    /// <summary>MIDI channel as displayed (1-16).</summary>
    public int Channel { get; set; } = 1;

    /// <summary>Note or controller number (0-127).</summary>
    public int Number { get; set; }

    /// <summary>Velocity or controller value (0-127).</summary>
    public int Value { get; set; } = MaxMidiValue;

    /// <summary>When true, sends Value and ValueB alternately (on/off toggle).</summary>
    public bool Toggle { get; set; }

    /// <summary>Second value for the toggle.</summary>
    public int ValueB { get; set; }

    /// <summary>Extra info, e.g. what the channel means on the device.</summary>
    public string? Hint { get; set; }

    /// <summary>Where the information comes from (manual, forum, own measurement).</summary>
    public string? Source { get; set; }

    /// <summary>True when the command was tested and confirmed.</summary>
    public bool Verified { get; set; }

    /// <summary>The <see cref="Kind"/> string as enum; anything but "NoteOn" counts as control change.</summary>
    [JsonIgnore]
    public MidiTriggerKind ParsedKind => this.Kind.Equals(NoteOnKind, StringComparison.OrdinalIgnoreCase)
        ? MidiTriggerKind.NoteOn
        : MidiTriggerKind.ControlChange;

    /// <summary>Text for the selection list; untested commands are marked.</summary>
    [JsonIgnore]
    public string DisplayName => this.Verified ? this.Name : $"{this.Name}  (ungetestet)";

    /// <summary>True when this entry describes the same command, used to update existing entries.</summary>
    public bool SameCommandAs(MidiPresetCommand other) =>
        this.ParsedKind == other.ParsedKind
        && this.Channel == other.Channel
        && this.Number == other.Number
        && this.Value == other.Value
        && this.Toggle == other.Toggle
        && (!this.Toggle || this.ValueB == other.ValueB);
}
