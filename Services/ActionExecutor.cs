using System.Diagnostics;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using HotKeyManager.Helpers;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

public class ActionExecutor
{
    private readonly HttpClient _httpClient = new();
    private readonly InterceptionService? _interceptionService;

    public ActionExecutor() { }

    public ActionExecutor(InterceptionService interceptionService)
    {
        _interceptionService = interceptionService;
    }
    
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    private const uint KEYEVENTF_KEYDOWN = 0x0000;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint INPUT_KEYBOARD = 1;
    private const uint WM_CHAR = 0x0102;
    
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }
    
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public KEYBDINPUT ki;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }
    
    /// <summary>
    /// Fuehrt eine Aktion aus (Standard-Modus, sendet an aktives Fenster).
    /// </summary>
    public async Task ExecuteAsync(ActionBase action)
    {
        await ExecuteAsync(action, WindowTargetMode.None, null, null);
    }
    
    /// <summary>
    /// Fuehrt eine Aktion aus mit optionalem Fenster-Targeting.
    /// </summary>
    /// <param name="action">Die auszufuehrende Aktion.</param>
    /// <param name="windowMode">Fenster-Targeting Modus.</param>
    /// <param name="targetProcessName">Zielprozess-Name (fuer SendToBackground).</param>
    /// <param name="targetWindowTitle">Zielfenster-Titel Pattern (fuer SendToBackground).</param>
    public async Task ExecuteAsync(ActionBase action, WindowTargetMode windowMode, string? targetProcessName, string? targetWindowTitle)
    {
        switch (action)
        {
            case WebhookAction webhook:
                await ExecuteWebhookAsync(webhook);
                break;
            case KeySequenceAction keySequence:
                if (windowMode == WindowTargetMode.SendToBackground)
                {
                    await ExecuteKeySequenceToWindowAsync(keySequence, targetProcessName, targetWindowTitle);
                }
                else if (keySequence.UseKernelInjection && _interceptionService?.CanSendKernel == true)
                {
                    await ExecuteKeySequenceKernelAsync(keySequence);
                }
                else
                {
                    await ExecuteKeySequenceAsync(keySequence);
                }
                break;
            case ProcessAction process:
                ExecuteProcess(process);
                break;
            case BatchAction batch:
                ExecuteBatch(batch);
                break;
            case SendTextAction sendText:
                if (windowMode == WindowTargetMode.SendToBackground)
                {
                    await ExecuteSendTextToWindowAsync(sendText, targetProcessName, targetWindowTitle);
                }
                else
                {
                    await ExecuteSendTextAsync(sendText);
                }
                break;
        }
    }
    
    private async Task ExecuteWebhookAsync(WebhookAction action)
    {
        try
        {
            var request = new HttpRequestMessage
            {
                RequestUri = new Uri(action.Url),
                Method = action.Method switch
                {
                    HttpMethodType.GET => HttpMethod.Get,
                    HttpMethodType.POST => HttpMethod.Post,
                    HttpMethodType.PUT => HttpMethod.Put,
                    HttpMethodType.DELETE => HttpMethod.Delete,
                    HttpMethodType.PATCH => HttpMethod.Patch,
                    _ => HttpMethod.Get
                }
            };
            
            // Add headers
            foreach (var header in action.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
            
            // Add body for POST/PUT/PATCH
            if (!string.IsNullOrEmpty(action.Body) && 
                action.Method is HttpMethodType.POST or HttpMethodType.PUT or HttpMethodType.PATCH)
            {
                request.Content = new StringContent(action.Body, Encoding.UTF8, action.ContentType);
            }
            
            await _httpClient.SendAsync(request);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Webhook error: {ex.Message}");
        }
    }
    
    private async Task ExecuteKeySequenceAsync(KeySequenceAction action)
    {
        foreach (var keyStroke in action.Keys)
        {
            switch (keyStroke.EventType)
            {
                case KeyEventType.KeyPress:
                    // Standard-Verhalten: Modifiers + Key Down + Key Up
                    await ExecuteKeyPressAsync(keyStroke);
                    break;
                    
                case KeyEventType.KeyDown:
                    // Nur Key Down (für Advanced Mode)
                    keybd_event((byte)keyStroke.VirtualKeyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                    break;
                    
                case KeyEventType.KeyUp:
                    // Nur Key Up (für Advanced Mode)
                    keybd_event((byte)keyStroke.VirtualKeyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                    break;
            }
            
            if (keyStroke.DelayAfterMs > 0)
            {
                await Task.Delay(keyStroke.DelayAfterMs);
            }
        }
    }
    
    /// <summary>
    /// Sendet eine Tastensequenz über den Kernel-Treiber (kein LLKHF_INJECTED-Flag).
    /// Wird von Apps erkannt, die synthetische Tastatureingaben filtern.
    /// </summary>
    private async Task ExecuteKeySequenceKernelAsync(KeySequenceAction action)
    {
        const int VK_CONTROL = 0x11;
        const int VK_ALT = 0x12;
        const int VK_SHIFT = 0x10;
        const int VK_LWIN = 0x5B;

        foreach (var keyStroke in action.Keys)
        {
            switch (keyStroke.EventType)
            {
                case KeyEventType.KeyPress:
                    // Modifier-Tasten drücken
                    if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Control))
                        _interceptionService!.KernelSimulateKeyDown(VK_CONTROL);
                    if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Alt))
                        _interceptionService!.KernelSimulateKeyDown(VK_ALT);
                    if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Shift))
                        _interceptionService!.KernelSimulateKeyDown(VK_SHIFT);
                    if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Windows))
                        _interceptionService!.KernelSimulateKeyDown(VK_LWIN);

                    // Taste drücken und loslassen
                    _interceptionService!.KernelSimulateKeyPress(keyStroke.VirtualKeyCode, 10);

                    // Modifier-Tasten loslassen
                    if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Windows))
                        _interceptionService!.KernelSimulateKeyUp(VK_LWIN);
                    if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Shift))
                        _interceptionService!.KernelSimulateKeyUp(VK_SHIFT);
                    if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Alt))
                        _interceptionService!.KernelSimulateKeyUp(VK_ALT);
                    if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Control))
                        _interceptionService!.KernelSimulateKeyUp(VK_CONTROL);
                    break;

                case KeyEventType.KeyDown:
                    _interceptionService!.KernelSimulateKeyDown(keyStroke.VirtualKeyCode);
                    break;

                case KeyEventType.KeyUp:
                    _interceptionService!.KernelSimulateKeyUp(keyStroke.VirtualKeyCode);
                    break;
            }

            if (keyStroke.DelayAfterMs > 0)
            {
                await Task.Delay(keyStroke.DelayAfterMs);
            }
        }
    }

    /// <summary>
    /// Sendet eine Tastensequenz an ein bestimmtes Fenster (auch im Hintergrund).
    /// </summary>
    private async Task ExecuteKeySequenceToWindowAsync(KeySequenceAction action, string? processName, string? titlePattern)
    {
        // Zielfenster finden
        var hwnd = WindowHelper.FindWindowByProcessAndTitle(processName, titlePattern);
        if (hwnd == IntPtr.Zero)
        {
            Debug.WriteLine($"Zielfenster nicht gefunden: Process={processName}, Title={titlePattern}");
            return;
        }
        
        foreach (var keyStroke in action.Keys)
        {
            switch (keyStroke.EventType)
            {
                case KeyEventType.KeyPress:
                    // Modifiers + Key Down + Key Up an Fenster senden
                    await ExecuteKeyPressToWindowAsync(keyStroke, hwnd);
                    break;
                    
                case KeyEventType.KeyDown:
                    WindowHelper.SendKeyToWindow(hwnd, keyStroke.VirtualKeyCode, isKeyDown: true);
                    break;
                    
                case KeyEventType.KeyUp:
                    WindowHelper.SendKeyToWindow(hwnd, keyStroke.VirtualKeyCode, isKeyDown: false);
                    break;
            }
            
            if (keyStroke.DelayAfterMs > 0)
            {
                await Task.Delay(keyStroke.DelayAfterMs);
            }
        }
    }
    
    /// <summary>
    /// Sendet einen einzelnen Tastendruck (mit Modifiers) an ein bestimmtes Fenster.
    /// </summary>
    private async Task ExecuteKeyPressToWindowAsync(KeyStroke keyStroke, IntPtr hwnd)
    {
        // Modifier-Tasten VK-Codes
        const int VK_CONTROL = 0x11;
        const int VK_ALT = 0x12;
        const int VK_SHIFT = 0x10;
        const int VK_LWIN = 0x5B;
        
        // Press modifiers
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Control))
            WindowHelper.SendKeyToWindow(hwnd, VK_CONTROL, isKeyDown: true);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Alt))
            WindowHelper.SendKeyToWindow(hwnd, VK_ALT, isKeyDown: true);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Shift))
            WindowHelper.SendKeyToWindow(hwnd, VK_SHIFT, isKeyDown: true);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Windows))
            WindowHelper.SendKeyToWindow(hwnd, VK_LWIN, isKeyDown: true);
        
        // Press and release the key
        WindowHelper.SendKeyToWindow(hwnd, keyStroke.VirtualKeyCode, isKeyDown: true);
        await Task.Delay(10);
        WindowHelper.SendKeyToWindow(hwnd, keyStroke.VirtualKeyCode, isKeyDown: false);
        
        // Release modifiers
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Windows))
            WindowHelper.SendKeyToWindow(hwnd, VK_LWIN, isKeyDown: false);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Shift))
            WindowHelper.SendKeyToWindow(hwnd, VK_SHIFT, isKeyDown: false);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Alt))
            WindowHelper.SendKeyToWindow(hwnd, VK_ALT, isKeyDown: false);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Control))
            WindowHelper.SendKeyToWindow(hwnd, VK_CONTROL, isKeyDown: false);
    }
    
    private async Task ExecuteKeyPressAsync(KeyStroke keyStroke)
    {
        // Press modifiers
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Control))
            keybd_event(0x11, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Alt))
            keybd_event(0x12, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Shift))
            keybd_event(0x10, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Windows))
            keybd_event(0x5B, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        
        // Press and release the key
        keybd_event((byte)keyStroke.VirtualKeyCode, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
        await Task.Delay(10);
        keybd_event((byte)keyStroke.VirtualKeyCode, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        
        // Release modifiers
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Windows))
            keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Shift))
            keybd_event(0x10, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Alt))
            keybd_event(0x12, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        if (keyStroke.Modifiers.HasFlag(Models.ModifierKeys.Control))
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }
    
    private void ExecuteProcess(ProcessAction action)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = action.FilePath,
                Arguments = action.Arguments,
                UseShellExecute = true,
                CreateNoWindow = action.Hidden,
                WindowStyle = action.Hidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };
            
            if (!string.IsNullOrEmpty(action.WorkingDirectory))
            {
                startInfo.WorkingDirectory = action.WorkingDirectory;
            }
            
            if (action.RunAsAdmin)
            {
                startInfo.Verb = "runas";
            }
            
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Process start error: {ex.Message}");
        }
    }
    
    private void ExecuteBatch(BatchAction action)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {action.Command}",
                UseShellExecute = false,
                CreateNoWindow = action.RunHidden,
                WindowStyle = action.RunHidden ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal
            };
            
            if (!string.IsNullOrEmpty(action.WorkingDirectory))
            {
                startInfo.WorkingDirectory = action.WorkingDirectory;
            }
            
            var process = Process.Start(startInfo);
            
            if (action.WaitForExit && process != null)
            {
                process.WaitForExit();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Batch command error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Sendet Text als simulierte Tastatureingabe mit SendInput (Unicode).
    /// </summary>
    private async Task ExecuteSendTextAsync(SendTextAction action)
    {
        if (string.IsNullOrEmpty(action.Text))
            return;
        
        try
        {
            foreach (char c in action.Text)
            {
                // Fuer jeden Buchstaben: KeyDown + KeyUp mit Unicode
                var inputs = new INPUT[2];
                
                // Key Down
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = 0;
                inputs[0].u.ki.wScan = c;
                inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;
                inputs[0].u.ki.time = 0;
                inputs[0].u.ki.dwExtraInfo = UIntPtr.Zero;
                
                // Key Up
                inputs[1].type = INPUT_KEYBOARD;
                inputs[1].u.ki.wVk = 0;
                inputs[1].u.ki.wScan = c;
                inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
                inputs[1].u.ki.time = 0;
                inputs[1].u.ki.dwExtraInfo = UIntPtr.Zero;
                
                SendInput(2, inputs, Marshal.SizeOf<INPUT>());

                // Verzoegerung zwischen Zeichen (konfigurierbare Tippgeschwindigkeit)
                if (action.CharDelayMs > 0)
                    await Task.Delay(action.CharDelayMs);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SendText error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Sendet Text an ein bestimmtes Fenster (auch im Hintergrund) via WM_CHAR.
    /// </summary>
    private async Task ExecuteSendTextToWindowAsync(SendTextAction action, string? processName, string? titlePattern)
    {
        if (string.IsNullOrEmpty(action.Text))
            return;
        
        // Zielfenster finden
        var hwnd = WindowHelper.FindWindowByProcessAndTitle(processName, titlePattern);
        if (hwnd == IntPtr.Zero)
        {
            Debug.WriteLine($"Zielfenster nicht gefunden: Process={processName}, Title={titlePattern}");
            return;
        }
        
        try
        {
            foreach (char c in action.Text)
            {
                // WM_CHAR senden fuer jeden Buchstaben
                PostMessage(hwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);

                // Verzoegerung zwischen Zeichen (konfigurierbare Tippgeschwindigkeit)
                if (action.CharDelayMs > 0)
                    await Task.Delay(action.CharDelayMs);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"SendTextToWindow error: {ex.Message}");
        }
    }
}
