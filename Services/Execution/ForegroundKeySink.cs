using static HotKeyManager.Services.Execution.KeyboardNative;

namespace HotKeyManager.Services.Execution;

/// <summary>Sends keys to whatever window has the focus (keybd_event, marked as injected).</summary>
internal sealed class ForegroundKeySink : IKeySink
{
    public static ForegroundKeySink Instance { get; } = new();

    public void KeyDown(int virtualKeyCode) => keybd_event((byte)virtualKeyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);

    public void KeyUp(int virtualKeyCode) => keybd_event((byte)virtualKeyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

    /// <summary>A press without hold time, enough for media keys.</summary>
    public void Tap(byte virtualKeyCode)
    {
        this.KeyDown(virtualKeyCode);
        this.KeyUp(virtualKeyCode);
    }
}
