using System.Diagnostics;
using System.Runtime.InteropServices;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using static HotKeyManager.Services.KeyboardHookNative;

namespace HotKeyManager.Services;

/// <summary>Global low-level keyboard hook that reports key presses and can swallow them.</summary>
public class KeyboardHookService : IDisposable
{
    // A non-zero return value from the hook callback stops the key from reaching other apps.
    private static readonly IntPtr BlockKeyResult = 1;

    private IntPtr hookId = IntPtr.Zero;

    // Kept in a field so the GC does not collect the delegate while Windows still calls it.
    private LowLevelKeyboardProc? proc;
    private bool disposed;

    ~KeyboardHookService() => this.Dispose();

    /// <summary>Raised on key down; handlers may set <see cref="KeyEventArgs.Handled"/> to block the key.</summary>
    public event EventHandler<KeyEventArgs>? KeyPressed;

    /// <summary>Raised on key up.</summary>
    public event EventHandler<KeyEventArgs>? KeyReleased;

    /// <summary>True while the hook is installed.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>True while the UI records keys; hotkeys do not fire meanwhile.</summary>
    public bool IsCapturing { get; set; }

    /// <summary>
    /// When true, modifier keys (Ctrl, Alt, Shift, Win) are reported as separate events.
    /// Used by the advanced mode of the key sequence recorder.
    /// </summary>
    public bool CaptureModifierKeys { get; set; }

    /// <summary>Installs the hook.</summary>
    /// <exception cref="InvalidOperationException">Windows refused to install the hook.</exception>
    public void Start()
    {
        if (this.IsRunning)
        {
            return;
        }

        this.hookId = this.InstallHook(out var error);
        if (this.hookId == IntPtr.Zero)
        {
            App.Current.LogService.Error($"Keyboard-Hook konnte nicht gesetzt werden (Win32-Error: {error})");
            throw new InvalidOperationException($"Failed to set keyboard hook. Error: {error}");
        }

        this.IsRunning = true;
        App.Current.LogService.Info("Keyboard-Hook gestartet");
    }

    /// <summary>Removes the hook.</summary>
    public void Stop()
    {
        if (!this.IsRunning || (this.hookId == IntPtr.Zero))
        {
            return;
        }

        UnhookWindowsHookEx(this.hookId);
        this.hookId = IntPtr.Zero;
        this.IsRunning = false;
    }

    /// <summary>
    /// Removes the hook and installs it again right away. Windows silently drops low-level hooks,
    /// e.g. after a callback timeout, sleep/wake or lock/unlock.
    /// </summary>
    public void Reinstall()
    {
        if (!this.IsRunning)
        {
            return;
        }

        if (this.hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(this.hookId);
            this.hookId = IntPtr.Zero;
        }

        this.hookId = this.InstallHook(out var error);
        if (this.hookId == IntPtr.Zero)
        {
            this.IsRunning = false;
            App.Current.LogService.Error($"Hook-Reinstall fehlgeschlagen (Win32-Error: {error})");
        }
        else
        {
            App.Current.LogService.Debug("Keyboard-Hook reinstalliert");
        }
    }

    /// <summary>Reads the modifier keys that are physically held right now.</summary>
    public ModifierKeys GetCurrentModifiers()
    {
        var modifiers = ModifierKeys.None;

        if (IsAnyKeyDown(KeyHelper.VK.VK_CONTROL, KeyHelper.VK.VK_LCONTROL, KeyHelper.VK.VK_RCONTROL))
        {
            modifiers |= ModifierKeys.Control;
        }

        if (IsAnyKeyDown(KeyHelper.VK.VK_MENU, KeyHelper.VK.VK_LMENU, KeyHelper.VK.VK_RMENU))
        {
            modifiers |= ModifierKeys.Alt;
        }

        if (IsAnyKeyDown(KeyHelper.VK.VK_SHIFT, KeyHelper.VK.VK_LSHIFT, KeyHelper.VK.VK_RSHIFT))
        {
            modifiers |= ModifierKeys.Shift;
        }

        if (IsAnyKeyDown(KeyHelper.VK.VK_LWIN, KeyHelper.VK.VK_RWIN))
        {
            modifiers |= ModifierKeys.Windows;
        }

        return modifiers;
    }

    /// <summary>Removes the hook.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.Stop();
        this.disposed = true;
        GC.SuppressFinalize(this);
    }

    private static bool IsAnyKeyDown(params int[] virtualKeyCodes) =>
        virtualKeyCodes.Any(virtualKeyCode => (GetAsyncKeyState(virtualKeyCode) & KeyDownMask) != 0);

    private IntPtr InstallHook(out int error)
    {
        this.proc = this.HookCallback;
        using var currentProcess = Process.GetCurrentProcess();
        using var currentModule = currentProcess.MainModule!;
        var hook = SetWindowsHookEx(WH_KEYBOARD_LL, this.proc, GetModuleHandle(currentModule.ModuleName!), 0);

        // Read the error before the disposals above run, they may overwrite it.
        error = Marshal.GetLastWin32Error();
        return hook;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // A negative code means the message must be passed on untouched.
        if (nCode < 0)
        {
            return CallNextHookEx(this.hookId, nCode, wParam, lParam);
        }

        var vkCode = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam).vkCode;

        // Modifiers are reported as part of the main key, unless the recorder wants them separately.
        var isModifier = KeyHelper.IsModifierKey(vkCode);
        if (isModifier && !this.CaptureModifierKeys)
        {
            return CallNextHookEx(this.hookId, nCode, wParam, lParam);
        }

        var modifiers = isModifier ? ModifierKeys.None : this.GetCurrentModifiers();
        var keyArgs = new KeyEventArgs(vkCode, modifiers);

        if (wParam is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            this.KeyPressed?.Invoke(this, keyArgs);
            if (keyArgs.Handled)
            {
                return BlockKeyResult;
            }
        }
        else if (wParam is WM_KEYUP or WM_SYSKEYUP)
        {
            this.KeyReleased?.Invoke(this, keyArgs);
        }

        return CallNextHookEx(this.hookId, nCode, wParam, lParam);
    }
}
