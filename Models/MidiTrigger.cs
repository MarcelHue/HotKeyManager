using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

public enum MidiTriggerKind
{
    /// <summary>Note-On-Nachricht (typisch fuer Pads, z.B. Rodecaster SMART Pads).</summary>
    NoteOn,
    /// <summary>Control-Change-Nachricht.</summary>
    ControlChange
}

/// <summary>
/// MIDI-Ausloeser fuer einen Hotkey (zusaetzlich oder alternativ zur Tastenkombination).
/// </summary>
public class MidiTrigger
{
    public MidiTriggerKind Kind { get; set; } = MidiTriggerKind.NoteOn;

    /// <summary>MIDI-Kanal 0-15 (Anzeige: 1-16).</summary>
    public byte Channel { get; set; }

    /// <summary>Note-Nummer (NoteOn) bzw. Controller-Nummer (ControlChange), 0-127.</summary>
    public byte Number { get; set; }

    [JsonIgnore]
    public string DisplayText => Kind == MidiTriggerKind.NoteOn
        ? $"Note {Number} · Kanal {Channel + 1}"
        : $"CC {Number} · Kanal {Channel + 1}";

    public bool Matches(MidiTriggerKind kind, byte channel, byte number) =>
        Kind == kind && Channel == channel && Number == number;
}
