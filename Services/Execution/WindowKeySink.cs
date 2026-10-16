using HotKeyManager.Helpers;

namespace HotKeyManager.Services.Execution;

/// <summary>Posts keys to one window, which also works while it is in the background.</summary>
internal sealed class WindowKeySink(IntPtr windowHandle) : IKeySink
{
    public void KeyDown(int virtualKeyCode) =>
        WindowHelper.SendKeyToWindow(windowHandle, virtualKeyCode, isKeyDown: true);

    public void KeyUp(int virtualKeyCode) =>
        WindowHelper.SendKeyToWindow(windowHandle, virtualKeyCode, isKeyDown: false);
}
