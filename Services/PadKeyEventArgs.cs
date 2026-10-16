namespace HotKeyManager.Services;

/// <summary>A claimed pad key was pressed or released.</summary>
public sealed class PadKeyEventArgs(PadKeyInfo key, bool pressed) : EventArgs
{
    public PadKeyInfo Key { get; } = key;

    public bool Pressed { get; } = pressed;
}
