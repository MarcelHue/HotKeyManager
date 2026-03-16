using System.Diagnostics;
using System.Runtime.InteropServices;
using HotKeyManager.Helpers;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

public class KeyboardHookService : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }
    
    private IntPtr _hookId = IntPtr.Zero;
    private LowLevelKeyboardProc? _proc;
    private bool _isRunning;
    private bool _disposed;
    
    public event EventHandler<KeyEventArgs>? KeyPressed;
    public event EventHandler<KeyEventArgs>? KeyReleased;
    
    public bool IsRunning => _isRunning;
    public bool IsCapturing { get; set; }
    
    /// <summary>
    /// Wenn true, werden auch Modifier-Tasten (Ctrl, Alt, Shift, Win) als separate Events aufgezeichnet.
    /// Wird für den Advanced Mode der Tastensequenz-Aufnahme verwendet.
    /// </summary>
    public bool CaptureModifierKeys { get; set; }
    
    public void Start()
    {
        if (_isRunning) return;
        
        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule.ModuleName!), 0);
        
        if (_hookId == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            App.Current.LogService.Error($"Keyboard-Hook konnte nicht gesetzt werden (Win32-Error: {error})");
            throw new InvalidOperationException($"Failed to set keyboard hook. Error: {error}");
        }
        
        _isRunning = true;
        App.Current.LogService.Info("Keyboard-Hook gestartet");
    }
    
    public void Stop()
    {
        if (!_isRunning || _hookId == IntPtr.Zero) return;
        
        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
        _isRunning = false;
    }
    
    public ModifierKeys GetCurrentModifiers()
    {
        var modifiers = ModifierKeys.None;
        
        if ((GetAsyncKeyState(KeyHelper.VK.VK_CONTROL) & 0x8000) != 0 ||
            (GetAsyncKeyState(KeyHelper.VK.VK_LCONTROL) & 0x8000) != 0 ||
            (GetAsyncKeyState(KeyHelper.VK.VK_RCONTROL) & 0x8000) != 0)
        {
            modifiers |= ModifierKeys.Control;
        }
        
        if ((GetAsyncKeyState(KeyHelper.VK.VK_MENU) & 0x8000) != 0 ||
            (GetAsyncKeyState(KeyHelper.VK.VK_LMENU) & 0x8000) != 0 ||
            (GetAsyncKeyState(KeyHelper.VK.VK_RMENU) & 0x8000) != 0)
        {
            modifiers |= ModifierKeys.Alt;
        }
        
        if ((GetAsyncKeyState(KeyHelper.VK.VK_SHIFT) & 0x8000) != 0 ||
            (GetAsyncKeyState(KeyHelper.VK.VK_LSHIFT) & 0x8000) != 0 ||
            (GetAsyncKeyState(KeyHelper.VK.VK_RSHIFT) & 0x8000) != 0)
        {
            modifiers |= ModifierKeys.Shift;
        }
        
        if ((GetAsyncKeyState(KeyHelper.VK.VK_LWIN) & 0x8000) != 0 ||
            (GetAsyncKeyState(KeyHelper.VK.VK_RWIN) & 0x8000) != 0)
        {
            modifiers |= ModifierKeys.Windows;
        }
        
        return modifiers;
    }
    
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var vkCode = hookStruct.vkCode;
            
            // Skip modifier keys for main key detection (unless CaptureModifierKeys is enabled)
            var isModifier = KeyHelper.IsModifierKey(vkCode);
            if (!isModifier || CaptureModifierKeys)
            {
                var modifiers = isModifier ? ModifierKeys.None : GetCurrentModifiers();
                var keyArgs = new KeyEventArgs(vkCode, modifiers);
                
                if (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN)
                {
                    KeyPressed?.Invoke(this, keyArgs);
                    
                    if (keyArgs.Handled)
                    {
                        return (IntPtr)1; // Block the key
                    }
                }
                else if (wParam == WM_KEYUP || wParam == WM_SYSKEYUP)
                {
                    KeyReleased?.Invoke(this, keyArgs);
                }
            }
        }
        
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
    
    ~KeyboardHookService()
    {
        Dispose();
    }
}

public class KeyEventArgs : EventArgs
{
    public int VirtualKeyCode { get; }
    public ModifierKeys Modifiers { get; }
    public bool Handled { get; set; }
    
    public string KeyDisplayText
    {
        get
        {
            var parts = new List<string>();
            
            if (Modifiers.HasFlag(ModifierKeys.Control))
                parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Alt))
                parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift))
                parts.Add("Shift");
            if (Modifiers.HasFlag(ModifierKeys.Windows))
                parts.Add("Win");
            
            parts.Add(KeyHelper.GetKeyName(VirtualKeyCode));
            
            return string.Join(" + ", parts);
        }
    }
    
    public KeyEventArgs(int virtualKeyCode, ModifierKeys modifiers)
    {
        VirtualKeyCode = virtualKeyCode;
        Modifiers = modifiers;
    }
}
