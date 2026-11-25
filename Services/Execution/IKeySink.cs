namespace HotKeyManager.Services.Execution;

/// <summary>Where simulated key presses go: the foreground app or one background window.</summary>
internal interface IKeySink
{
    void KeyDown(int virtualKeyCode);

    void KeyUp(int virtualKeyCode);
}
