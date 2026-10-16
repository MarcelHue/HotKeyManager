namespace HotKeyManager.Models;

/// <summary>
/// Sends a MIDI message to an output device, e.g. back to the Rodecaster,
/// to a DAW or to a virtual MIDI cable.
/// </summary>
public class MidiSendAction : ActionBase
{
    private const byte MiddleC = 60;
    private const byte MaxMidiValue = 127;
    private const int DefaultNoteOffAfterMs = 50;

    public override ActionType Type => ActionType.MidiSend;

    public override string DisplayName => "MIDI senden";

    public override string Description
    {
        get
        {
            // A preset command name says more than the raw MIDI values.
            if (!string.IsNullOrEmpty(this.PresetCommandName))
            {
                return string.IsNullOrEmpty(this.PresetName)
                    ? this.PresetCommandName!
                    : $"{this.PresetName}: {this.PresetCommandName}";
            }

            var type = this.Kind == MidiTriggerKind.NoteOn ? "Note" : "CC";
            return this.ToggleValues
                ? $"{type} {this.Number} · Wert {this.Value} ↔ {this.ValueB} · Kanal {this.Channel + 1}"
                : $"{type} {this.Number} · Wert {this.Value} · Kanal {this.Channel + 1}";
        }
    }

    /// <summary>Name of the preset the command comes from, for display only.</summary>
    public string? PresetName { get; set; }

    /// <summary>Name of the preset command, for display only.</summary>
    public string? PresetCommandName { get; set; }

    /// <summary>Name of the MIDI output device; empty means the first available device.</summary>
    public string DeviceName { get; set; } = string.Empty;

    public MidiTriggerKind Kind { get; set; } = MidiTriggerKind.NoteOn;

    /// <summary>MIDI channel 0-15 (displayed as 1-16).</summary>
    public byte Channel { get; set; }

    /// <summary>Note or controller number, 0-127.</summary>
    public byte Number { get; set; } = MiddleC;

    /// <summary>Velocity (NoteOn) or controller value (CC), 0-127.</summary>
    public byte Value { get; set; } = MaxMidiValue;

    /// <summary>
    /// When true, each run sends Value and ValueB alternately. For devices with separate on/off
    /// commands whose state cannot be read back (e.g. Rodecaster: CC 27 value 1 = muted, 0 = unmuted).
    /// </summary>
    public bool ToggleValues { get; set; }

    /// <summary>Second value for toggle mode.</summary>
    public byte ValueB { get; set; }

    /// <summary>
    /// Which value was sent last. Persisted so the toggle keeps alternating after a restart;
    /// updated on every run.
    /// </summary>
    public bool LastSentWasB { get; set; }

    /// <summary>For NoteOn: send the matching note-off after this time (0 = none).</summary>
    public int NoteOffAfterMs { get; set; } = DefaultNoteOffAfterMs;
}
