namespace HotKeyManager.Models;

/// <summary>Modifier keys of a hotkey; the values are bit flags persisted in config.json.</summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8
}
