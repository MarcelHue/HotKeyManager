namespace HotKeyManager.Models;

/// <summary>The kinds of actions a hotkey can run.</summary>
public enum ActionType
{
    Webhook,
    KeySequence,
    StartProcess,
    BatchCommand,
    SendText,
    Delay,
    Macro,
    System,
    MidiSend
}
