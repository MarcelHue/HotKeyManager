using System.Runtime.InteropServices;
using InputInterceptorNS;

namespace HotKeyManager.Services;

/// <summary>
/// Service für Kernel-Mode Tastatur-Injektion mittels des Interception-Treibers.
/// Ermöglicht das Senden von Tasten ohne LLKHF_INJECTED-Flag.
/// </summary>
public class InterceptionService : IDisposable
{
    private KeyboardHook? _keyboardHook;
    private bool _isRunning;
    private bool _disposed;

    /// <summary>
    /// Prüft ob der Interception-Treiber installiert ist (in Registry eingetragen).
    /// </summary>
    public static bool IsDriverInstalled()
    {
        try { return InputInterceptor.CheckDriverInstalled(); }
        catch { return false; }
    }

    /// <summary>
    /// Prüft ob der Interception-Treiber aktiv ist (geladen und funktionsfähig).
    /// </summary>
    public static bool IsDriverActive()
    {
        try
        {
            if (!IsDriverInstalled()) return false;
            return InputInterceptor.Initialize();
        }
        catch { return false; }
    }

    /// <summary>
    /// Startet einen Kindprozess mit Admin-Rechten für die Treiber-Installation/Deinstallation.
    /// </summary>
    private static async Task<bool> RunInstallAsAdminAsync(bool install)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exePath,
            Arguments = install ? "--install-driver" : "--uninstall-driver",
            Verb = "runas",
            UseShellExecute = true
        };

        try
        {
            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null) { await process.WaitForExitAsync(); return true; }
            return false;
        }
        catch { return false; }
    }

    public static async Task<(bool Success, string Message)> InstallDriverAsync()
    {
        try
        {
            if (IsDriverInstalled()) return (true, "Treiber ist bereits installiert.");

            if (IsRunningAsAdmin())
                await Task.Run(() => { try { InputInterceptor.InstallDriver(); } catch { } });
            else if (!await RunInstallAsAdminAsync(install: true))
                return (false, "Installation abgebrochen. Administrator-Rechte wurden verweigert.");

            return IsDriverInstalled()
                ? (true, "Treiber wurde installiert. Bitte starte den Computer neu.")
                : (false, "Installation fehlgeschlagen. Der Treiber wurde nicht korrekt registriert.");
        }
        catch (Exception ex) { return (false, $"Fehler bei der Treiber-Installation: {ex.Message}"); }
    }

    public static async Task<(bool Success, string Message)> UninstallDriverAsync()
    {
        try
        {
            if (!IsDriverInstalled()) return (true, "Treiber ist nicht installiert.");

            if (IsRunningAsAdmin())
                await Task.Run(() => { try { InputInterceptor.UninstallDriver(); } catch { } });
            else if (!await RunInstallAsAdminAsync(install: false))
                return (false, "Deinstallation abgebrochen. Administrator-Rechte wurden verweigert.");

            return !IsDriverInstalled()
                ? (true, "Treiber wurde deinstalliert. Bitte starte den Computer neu.")
                : (false, "Deinstallation fehlgeschlagen. Der Treiber ist noch registriert.");
        }
        catch (Exception ex) { return (false, $"Fehler bei der Treiber-Deinstallation: {ex.Message}"); }
    }

    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>
    /// Startet den Service (aktiviert Kernel-Injektion).
    /// </summary>
    public bool Start()
    {
        if (_isRunning) return true;
        if (!IsDriverActive()) return false;

        try
        {
            _keyboardHook = new KeyboardHook(PassThrough);
            _isRunning = true;
            return true;
        }
        catch { return false; }
    }

    private static void PassThrough(ref KeyStroke keyStroke) { }

    public void Stop()
    {
        if (!_isRunning) return;
        _keyboardHook?.Dispose();
        _keyboardHook = null;
        _isRunning = false;
    }

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);
    private const uint MAPVK_VK_TO_VSC = 0;

    /// <summary>True wenn der Kernel-Treiber aktiv ist und Tasten gesendet werden können.</summary>
    public bool CanSendKernel => _isRunning && _keyboardHook != null;

    public void KernelSimulateKeyDown(int virtualKeyCode)
    {
        if (_keyboardHook == null) return;
        var scanCode = (int)MapVirtualKey((uint)virtualKeyCode, MAPVK_VK_TO_VSC);
        if (scanCode == 0) scanCode = virtualKeyCode;
        _keyboardHook.SimulateKeyDown((KeyCode)scanCode);
    }

    public void KernelSimulateKeyUp(int virtualKeyCode)
    {
        if (_keyboardHook == null) return;
        var scanCode = (int)MapVirtualKey((uint)virtualKeyCode, MAPVK_VK_TO_VSC);
        if (scanCode == 0) scanCode = virtualKeyCode;
        _keyboardHook.SimulateKeyUp((KeyCode)scanCode);
    }

    public void KernelSimulateKeyPress(int virtualKeyCode, int delayMs = 10)
    {
        if (_keyboardHook == null) return;
        var scanCode = (int)MapVirtualKey((uint)virtualKeyCode, MAPVK_VK_TO_VSC);
        if (scanCode == 0) scanCode = virtualKeyCode;
        _keyboardHook.SimulateKeyPress((KeyCode)scanCode, delayMs);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~InterceptionService() => Dispose();
}
