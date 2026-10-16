namespace HotKeyManager.Models;

/// <summary>The MIDI message types the app can receive and send.</summary>
public enum MidiTriggerKind
{
    /// <summary>Note-on message, typical for pads such as the Rodecaster SMART pads.</summary>
    NoteOn,

    /// <summary>Control-change message.</summary>
    ControlChange
}
