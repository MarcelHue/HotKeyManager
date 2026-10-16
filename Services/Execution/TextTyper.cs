using System.Runtime.InteropServices;
using HotKeyManager.Models;
using static HotKeyManager.Services.Execution.KeyboardNative;

namespace HotKeyManager.Services.Execution;

/// <summary>Types text character by character, at the speed configured in the action.</summary>
internal static class TextTyper
{
    /// <summary>Into the focused window as Unicode key events, independent of the keyboard layout.</summary>
    public static async Task TypeIntoForegroundAsync(SendTextAction action)
    {
        foreach (var character in action.Text)
        {
            INPUT[] inputs =
            [
                UnicodeInput(character, KEYEVENTF_UNICODE),
                UnicodeInput(character, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP),
            ];
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            await DelayAsync(action);
        }
    }

    /// <summary>Into one window via WM_CHAR, which also works while it is in the background.</summary>
    public static async Task TypeIntoWindowAsync(SendTextAction action, IntPtr windowHandle)
    {
        foreach (var character in action.Text)
        {
            PostMessage(windowHandle, WM_CHAR, character, IntPtr.Zero);
            await DelayAsync(action);
        }
    }

    private static INPUT UnicodeInput(char character, uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wVk = 0, wScan = character, dwFlags = flags } },
    };

    private static Task DelayAsync(SendTextAction action) =>
        action.CharDelayMs > 0 ? Task.Delay(action.CharDelayMs) : Task.CompletedTask;
}
