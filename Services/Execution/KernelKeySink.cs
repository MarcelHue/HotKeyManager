namespace HotKeyManager.Services.Execution;

/// <summary>
/// Sends keys through the Interception kernel driver, so they carry no LLKHF_INJECTED flag.
/// Needed for apps that ignore synthetic keyboard input.
/// </summary>
internal sealed class KernelKeySink(InterceptionService interception) : IKeySink
{
    public void KeyDown(int virtualKeyCode) => interception.KernelSimulateKeyDown(virtualKeyCode);

    public void KeyUp(int virtualKeyCode) => interception.KernelSimulateKeyUp(virtualKeyCode);
}
