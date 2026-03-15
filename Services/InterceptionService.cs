using System.Diagnostics;
using System.Runtime.InteropServices;
using HotKeyManager.Helpers;
using HotKeyManager.Models;
using InputInterceptorNS;

namespace HotKeyManager.Services;

/// <summary>
/// Service für Kernel-Mode Keyboard Interception mittels des Interception-Treibers.
/// Ermöglicht das Abfangen von Tasten auch im Remote Desktop Vollbildmodus.
/// </summary>
public class InterceptionService : IDisposable
{
    private KeyboardHook? _keyboardHook;
    private bool _isRunning;
    private bool _disposed;
    private readonly List<HotkeyDefinition> _registeredHotkeys = new();
    private readonly object _lock = new();

    public event EventHandler<InterceptionKeyEventArgs>? HotkeyTriggered;

    /// <summary>
    /// Prüft ob der Interception-Treiber installiert ist (in Registry eingetragen).
    /// </summary>
    public static bool IsDriverInstalled()
    {
        try
        {
            return InputInterceptor.CheckDriverInstalled();
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prüft ob der Interception-Treiber aktiv ist (tatsächlich geladen und funktionsfähig).
    /// Der Treiber kann installiert sein, aber erst nach einem Neustart aktiv werden.
    /// </summary>
    public static bool IsDriverActive()
    {
        try
        {
            var isInstalled = IsDriverInstalled();
            if (!isInstalled)
                return false;

            // Bibliothek muss zuerst initialisiert werden
            var initialized = InputInterceptor.Initialize();
            return initialized;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Prüft ob die Anwendung mit Administrator-Rechten läuft.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Startet einen Kindprozess mit Admin-Rechten für die Treiber-Installation/Deinstallation.
    /// Zeigt einen UAC-Prompt an.
    /// </summary>
    /// <param name="install">True für Installation, False für Deinstallation</param>
    /// <returns>True wenn der Prozess erfolgreich gestartet und beendet wurde</returns>
    public static async Task<bool> RunInstallAsAdminAsync(bool install)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return false;

        var argument = install ? "--install-driver" : "--uninstall-driver";

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = argument,
            Verb = "runas",  // UAC-Prompt
            UseShellExecute = true
        };

        try
        {
            var process = Process.Start(startInfo);
            if (process != null)
            {
                await process.WaitForExitAsync();
                return true;
            }
            return false;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Benutzer hat UAC abgelehnt
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Installiert den Interception-Treiber. Erfordert Administrator-Rechte und einen Neustart.
    /// </summary>
    /// <returns>True wenn erfolgreich, false bei Fehler</returns>
    public static async Task<(bool Success, string Message)> InstallDriverAsync()
    {
        try
        {
            if (IsDriverInstalled())
            {
                return (true, "Treiber ist bereits installiert.");
            }

            if (IsRunningAsAdmin())
            {
                // Direkt installieren wenn bereits Admin
                await Task.Run(() =>
                {
                    try
                    {
                        InputInterceptor.InstallDriver();
                    }
                    catch { }
                });
            }
            else
            {
                // Als Admin-Kindprozess starten (zeigt UAC-Prompt)
                var uacAccepted = await RunInstallAsAdminAsync(install: true);
                if (!uacAccepted)
                {
                    return (false, "Installation abgebrochen. Administrator-Rechte wurden verweigert.");
                }
            }

            // Verifizieren ob Installation erfolgreich war
            if (IsDriverInstalled())
            {
                return (true, "Treiber wurde installiert. Bitte starte den Computer neu, um die Installation abzuschließen.");
            }
            else
            {
                return (false, "Installation fehlgeschlagen. Der Treiber wurde nicht korrekt registriert.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Fehler bei der Treiber-Installation: {ex.Message}");
        }
    }

    /// <summary>
    /// Deinstalliert den Interception-Treiber.
    /// </summary>
    public static async Task<(bool Success, string Message)> UninstallDriverAsync()
    {
        try
        {
            if (!IsDriverInstalled())
            {
                return (true, "Treiber ist nicht installiert.");
            }

            if (IsRunningAsAdmin())
            {
                // Direkt deinstallieren wenn bereits Admin
                await Task.Run(() =>
                {
                    try
                    {
                        InputInterceptor.UninstallDriver();
                    }
                    catch { }
                });
            }
            else
            {
                // Als Admin-Kindprozess starten (zeigt UAC-Prompt)
                var uacAccepted = await RunInstallAsAdminAsync(install: false);
                if (!uacAccepted)
                {
                    return (false, "Deinstallation abgebrochen. Administrator-Rechte wurden verweigert.");
                }
            }

            // Verifizieren ob Deinstallation erfolgreich war
            if (!IsDriverInstalled())
            {
                return (true, "Treiber wurde deinstalliert. Bitte starte den Computer neu.");
            }
            else
            {
                return (false, "Deinstallation fehlgeschlagen. Der Treiber ist noch registriert.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Fehler bei der Treiber-Deinstallation: {ex.Message}");
        }
    }

    /// <summary>
    /// Startet den Interception-Hook.
    /// </summary>
    public bool Start()
    {
        if (_isRunning) return true;
        
        // Pruefen ob Treiber aktiv ist (nicht nur installiert)
        if (!IsDriverActive())
        {
            Debug.WriteLine("InterceptionService: Treiber ist installiert aber nicht aktiv (Neustart erforderlich)");
            return false;
        }

        try
        {
            _keyboardHook = new KeyboardHook(OnKeyCallback);
            _isRunning = true;
            Debug.WriteLine("InterceptionService: Hook gestartet");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"InterceptionService: Fehler beim Starten: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Stoppt den Interception-Hook.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning) return;

        _keyboardHook?.Dispose();
        _keyboardHook = null;
        _isRunning = false;
        Debug.WriteLine("InterceptionService: Hook gestoppt");
    }

    /// <summary>
    /// Registriert einen Hotkey für die Kernel-Mode Interception.
    /// </summary>
    public void RegisterHotkey(HotkeyDefinition hotkey)
    {
        lock (_lock)
        {
            if (!_registeredHotkeys.Any(h => h.Id == hotkey.Id))
            {
                _registeredHotkeys.Add(hotkey);
                Debug.WriteLine($"InterceptionService: Hotkey registriert: {hotkey.Name}");
            }
        }
    }

    /// <summary>
    /// Deregistriert einen Hotkey.
    /// </summary>
    public void UnregisterHotkey(Guid hotkeyId)
    {
        lock (_lock)
        {
            var hotkey = _registeredHotkeys.FirstOrDefault(h => h.Id == hotkeyId);
            if (hotkey != null)
            {
                _registeredHotkeys.Remove(hotkey);
                Debug.WriteLine($"InterceptionService: Hotkey deregistriert: {hotkey.Name}");
            }
        }
    }

    /// <summary>
    /// Aktualisiert alle registrierten Hotkeys.
    /// </summary>
    public void UpdateRegisteredHotkeys(IEnumerable<HotkeyDefinition> hotkeys)
    {
        lock (_lock)
        {
            _registeredHotkeys.Clear();
            foreach (var hotkey in hotkeys.Where(h => h.UseKernelInterception && h.IsEnabled))
            {
                _registeredHotkeys.Add(hotkey);
            }
            Debug.WriteLine($"InterceptionService: {_registeredHotkeys.Count} Hotkeys registriert");
        }
    }

    /// <summary>
    /// Prüft ob ein Hotkey für Kernel-Mode registriert ist.
    /// </summary>
    public bool IsHotkeyRegistered(int virtualKeyCode, ModifierKeys modifiers)
    {
        lock (_lock)
        {
            return _registeredHotkeys.Any(h =>
                h.IsEnabled &&
                h.VirtualKeyCode == virtualKeyCode &&
                h.Modifiers == modifiers);
        }
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
    
    private const uint MAPVK_VSC_TO_VK_EX = 3;

    private ModifierKeys GetCurrentModifiers()
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

    private void OnKeyCallback(ref InputInterceptorNS.KeyStroke keyStroke)
    {
        // Nur KeyDown-Events verarbeiten (Down = 0x00, E0 + Down = 0x02)
        var state = (int)keyStroke.State;
        if (state != 0x00 && state != 0x02)
        {
            return;
        }

        var scanCode = (uint)keyStroke.Code;
        
        // Scan Code zu Virtual Key Code konvertieren
        // Bei E0-Tasten (state == 0x02) muss das E0-Flag gesetzt werden
        var scanCodeWithFlags = scanCode;
        if (state == 0x02)
        {
            scanCodeWithFlags |= 0xE000; // E0-Praefix
        }
        var vkCode = (int)MapVirtualKey(scanCodeWithFlags, MAPVK_VSC_TO_VK_EX);
        
        // Falls MapVirtualKey fehlschlaegt, Scan Code direkt verwenden
        if (vkCode == 0)
        {
            vkCode = (int)scanCode;
        }

        // Modifier-Tasten ignorieren
        if (KeyHelper.IsModifierKey(vkCode))
        {
            return;
        }

        var modifiers = GetCurrentModifiers();

        lock (_lock)
        {
            var matchingHotkey = _registeredHotkeys.FirstOrDefault(h =>
                h.IsEnabled &&
                h.VirtualKeyCode == vkCode &&
                h.Modifiers == modifiers);

            if (matchingHotkey != null)
            {
                Debug.WriteLine($"InterceptionService: Hotkey ausgelöst: {matchingHotkey.Name}");

                // Taste blockieren (nicht weiterleiten) - State auf Up setzen
                keyStroke.State = (InputInterceptorNS.KeyState)0x01;

                // Event auslösen
                HotkeyTriggered?.Invoke(this, new InterceptionKeyEventArgs(matchingHotkey));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~InterceptionService()
    {
        Dispose();
    }
}

/// <summary>
/// Event-Argumente für Interception-Hotkey-Events.
/// </summary>
public class InterceptionKeyEventArgs : EventArgs
{
    public HotkeyDefinition Hotkey { get; }

    public InterceptionKeyEventArgs(HotkeyDefinition hotkey)
    {
        Hotkey = hotkey;
    }
}
