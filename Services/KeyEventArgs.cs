using HotKeyManager.Helpers;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

/// <summary>A key event from the low-level keyboard hook; set <see cref="Handled"/> to swallow the key.</summary>
public class KeyEventArgs : EventArgs
{
    /// <summary>Creates the event data for one key.</summary>
    public KeyEventArgs(int virtualKeyCode, ModifierKeys modifiers)
    {
        this.VirtualKeyCode = virtualKeyCode;
        this.Modifiers = modifiers;
    }

    public int VirtualKeyCode { get; }

    /// <summary>Modifiers held at the time of the event; None for modifier keys themselves.</summary>
    public ModifierKeys Modifiers { get; }

    /// <summary>When set by a key-down handler, the key is not passed on to other applications.</summary>
    public bool Handled { get; set; }

    /// <summary>Display text such as "Ctrl + Shift + F5".</summary>
    public string KeyDisplayText
    {
        get
        {
            var parts = new List<string>();

            if (this.Modifiers.HasFlag(ModifierKeys.Control))
            {
                parts.Add("Ctrl");
            }

            if (this.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                parts.Add("Alt");
            }

            if (this.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                parts.Add("Shift");
            }

            if (this.Modifiers.HasFlag(ModifierKeys.Windows))
            {
                parts.Add("Win");
            }

            parts.Add(KeyHelper.GetKeyName(this.VirtualKeyCode));

            return string.Join(" + ", parts);
        }
    }
}
