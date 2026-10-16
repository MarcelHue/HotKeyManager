namespace HotKeyManager.Models;

/// <summary>What a single entry of a key sequence does with its key.</summary>
public enum KeyEventType
{
    /// <summary>Press and release the key (default, simple mode).</summary>
    KeyPress,

    /// <summary>Only press the key, to build combinations in advanced mode.</summary>
    KeyDown,

    /// <summary>Only release the key, to build combinations in advanced mode.</summary>
    KeyUp
}
