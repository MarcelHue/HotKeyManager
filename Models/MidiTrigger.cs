using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>MIDI trigger for a hotkey, in addition to or instead of the key combination.</summary>
public class MidiTrigger
{
    public MidiTriggerKind Kind { get; set; } = MidiTriggerKind.NoteOn;

    /// <summary>MIDI channel 0-15 (displayed as 1-16).</summary>
    public byte Channel { get; set; }

    /// <summary>Note number (NoteOn) or controller number (ControlChange), 0-127.</summary>
    public byte Number { get; set; }

    [JsonIgnore]
    public string DisplayText => this.Kind == MidiTriggerKind.NoteOn
        ? $"Note {this.Number} · Kanal {this.Channel + 1}"
        : $"CC {this.Number} · Kanal {this.Channel + 1}";

    /// <summary>True when the incoming MIDI message is this trigger.</summary>
    public bool Matches(MidiTriggerKind kind, byte channel, byte number) =>
        this.Kind == kind && this.Channel == channel && this.Number == number;
}
