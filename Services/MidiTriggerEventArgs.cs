using HotKeyManager.Models;

namespace HotKeyManager.Services;

/// <summary>An incoming MIDI message that can trigger a hotkey.</summary>
public class MidiTriggerEventArgs(MidiTriggerKind kind, byte channel, byte number, string deviceName) : EventArgs
{
    public MidiTriggerKind Kind { get; } = kind;

    /// <summary>MIDI channel 0-15 (shown as 1-16).</summary>
    public byte Channel { get; } = channel;

    /// <summary>Note number (NoteOn) or controller number (ControlChange).</summary>
    public byte Number { get; } = number;

    public string DeviceName { get; } = deviceName;
}
