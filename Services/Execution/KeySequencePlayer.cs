using HotKeyManager.Models;
using static HotKeyManager.Services.Execution.KeyboardNative;

namespace HotKeyManager.Services.Execution;

/// <summary>Plays a key sequence into a key sink, with the delays configured per stroke.</summary>
internal static class KeySequencePlayer
{
    /// <summary>How long the main key of a combination stays down; some apps miss shorter presses.</summary>
    private const int KeyHoldMs = 10;

    /// <summary>Modifiers in press order; they are released in reverse order.</summary>
    private static readonly (ModifierKeys Flag, int VirtualKeyCode)[] ModifierOrder =
    [
        (ModifierKeys.Control, VK_CONTROL),
        (ModifierKeys.Alt, VK_MENU),
        (ModifierKeys.Shift, VK_SHIFT),
        (ModifierKeys.Windows, VK_LWIN),
    ];

    public static async Task PlayAsync(KeySequenceAction action, IKeySink sink)
    {
        foreach (var keyStroke in action.Keys)
        {
            switch (keyStroke.EventType)
            {
                case KeyEventType.KeyPress:
                    await PressAsync(keyStroke, sink);
                    break;

                // KeyDown/KeyUp alone come from the advanced editor mode.
                case KeyEventType.KeyDown:
                    sink.KeyDown(keyStroke.VirtualKeyCode);
                    break;

                case KeyEventType.KeyUp:
                    sink.KeyUp(keyStroke.VirtualKeyCode);
                    break;
            }

            if (keyStroke.DelayAfterMs > 0)
            {
                await Task.Delay(keyStroke.DelayAfterMs);
            }
        }
    }

    private static async Task PressAsync(KeyStroke keyStroke, IKeySink sink)
    {
        var modifiers = ModifierOrder
            .Where(m => keyStroke.Modifiers.HasFlag(m.Flag))
            .Select(m => m.VirtualKeyCode)
            .ToList();

        foreach (var modifier in modifiers)
        {
            sink.KeyDown(modifier);
        }

        sink.KeyDown(keyStroke.VirtualKeyCode);
        await Task.Delay(KeyHoldMs);
        sink.KeyUp(keyStroke.VirtualKeyCode);

        for (var i = modifiers.Count - 1; i >= 0; i--)
        {
            sink.KeyUp(modifiers[i]);
        }
    }
}
