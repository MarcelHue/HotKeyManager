using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using InputInterceptorNS;

namespace HotKeyManager.Services;

/// <summary>
/// Kernel-mode keyboard injection through the Interception driver.
/// Keys sent this way carry no LLKHF_INJECTED flag, so apps that ignore synthetic input still see them.
/// </summary>
public class InterceptionService : IDisposable
{
    private const uint MAPVK_VK_TO_VSC = 0;

    // Must match the command line switches handled in App.xaml.cs.
    private const string InstallDriverArgument = "--install-driver";
    private const string UninstallDriverArgument = "--uninstall-driver";

    private KeyboardHook? keyboardHook;
    private bool disposed;

    ~InterceptionService() => this.Dispose();

    /// <summary>True after <see cref="Start"/> succeeded.</summary>
    public bool IsRunning { get; private set; }

    /// <summary>True when the kernel driver is active and keys can be sent.</summary>
    public bool CanSendKernel => this.IsRunning && (this.keyboardHook is not null);

    /// <summary>Checks whether the Interception driver is installed (registered in the registry).</summary>
    public static bool IsDriverInstalled()
    {
        try
        {
            return InputInterceptor.CheckDriverInstalled();
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("Fehler bei Treiber-Status Pruefung", ex);
            return false;
        }
    }

    /// <summary>Checks whether the Interception driver is loaded and working.</summary>
    public static bool IsDriverActive()
    {
        try
        {
            return IsDriverInstalled() && InputInterceptor.Initialize();
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("Fehler bei Treiber-Aktivitaetspruefung", ex);
            return false;
        }
    }

    /// <summary>Installs the driver, elevating through a child process if needed.</summary>
    /// <returns>Whether it worked and a German message for the user.</returns>
    public static async Task<(bool Success, string Message)> InstallDriverAsync()
    {
        try
        {
            if (IsDriverInstalled())
            {
                return (true, "Treiber ist bereits installiert.");
            }

            if (!await RunDriverSetupAsync(install: true))
            {
                return (false, "Installation abgebrochen. Administrator-Rechte wurden verweigert.");
            }

            return IsDriverInstalled()
                ? (true, "Treiber wurde installiert. Bitte starte den Computer neu.")
                : (false, "Installation fehlgeschlagen. Der Treiber wurde nicht korrekt registriert.");
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Treiber-Installation Fehler", ex);
            return (false, $"Fehler bei der Treiber-Installation: {ex.Message}");
        }
    }

    /// <summary>Uninstalls the driver, elevating through a child process if needed.</summary>
    /// <returns>Whether it worked and a German message for the user.</returns>
    public static async Task<(bool Success, string Message)> UninstallDriverAsync()
    {
        try
        {
            if (!IsDriverInstalled())
            {
                return (true, "Treiber ist nicht installiert.");
            }

            if (!await RunDriverSetupAsync(install: false))
            {
                return (false, "Deinstallation abgebrochen. Administrator-Rechte wurden verweigert.");
            }

            return !IsDriverInstalled()
                ? (true, "Treiber wurde deinstalliert. Bitte starte den Computer neu.")
                : (false, "Deinstallation fehlgeschlagen. Der Treiber ist noch registriert.");
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Treiber-Deinstallation Fehler", ex);
            return (false, $"Fehler bei der Treiber-Deinstallation: {ex.Message}");
        }
    }

    /// <summary>True when the current process runs elevated.</summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("Admin-Status konnte nicht geprueft werden", ex);
            return false;
        }
    }

    /// <summary>Opens the driver context for kernel injection.</summary>
    /// <returns>False when the driver is not active or the context could not be opened.</returns>
    public bool Start()
    {
        if (this.IsRunning)
        {
            return true;
        }

        if (!IsDriverActive())
        {
            return false;
        }

        try
        {
            this.keyboardHook = new KeyboardHook(PassThrough);
            this.IsRunning = true;
            App.Current.LogService.Info("Interception-Kernel-Service gestartet");
            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Interception-Kernel-Service konnte nicht gestartet werden", ex);
            return false;
        }
    }

    /// <summary>Releases the driver context.</summary>
    public void Stop()
    {
        if (!this.IsRunning)
        {
            return;
        }

        this.keyboardHook?.Dispose();
        this.keyboardHook = null;
        this.IsRunning = false;
    }

    /// <summary>Presses a key through the driver.</summary>
    public void KernelSimulateKeyDown(int virtualKeyCode) =>
        this.keyboardHook?.SimulateKeyDown(ToKeyCode(virtualKeyCode));

    /// <summary>Releases a key through the driver.</summary>
    public void KernelSimulateKeyUp(int virtualKeyCode) => this.keyboardHook?.SimulateKeyUp(ToKeyCode(virtualKeyCode));

    /// <summary>Stops the service.</summary>
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

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    // The driver works with scan codes. Keys without one keep the virtual key code as a best effort.
    private static KeyCode ToKeyCode(int virtualKeyCode)
    {
        var scanCode = (int)MapVirtualKey((uint)virtualKeyCode, MAPVK_VK_TO_VSC);
        return (KeyCode)(scanCode == 0 ? virtualKeyCode : scanCode);
    }

    // The hook is only needed to inject keys; every real key stroke passes through unchanged.
    private static void PassThrough(ref KeyStroke keyStroke)
    {
    }

    private static async Task<bool> RunDriverSetupAsync(bool install)
    {
        if (!IsRunningAsAdmin())
        {
            return await RunSetupAsAdminAsync(install);
        }

        await Task.Run(() => RunSetupInProcess(install));
        return true;
    }

    private static void RunSetupInProcess(bool install)
    {
        try
        {
            if (install)
            {
                InputInterceptor.InstallDriver();
            }
            else
            {
                InputInterceptor.UninstallDriver();
            }
        }
        catch (Exception ex)
        {
            var message = install ? "Treiber-Installation fehlgeschlagen" : "Treiber-Deinstallation fehlgeschlagen";
            App.Current.LogService.Error(message, ex);
        }
    }

    // Driver setup needs admin rights; the app restarts itself elevated for just that step.
    private static async Task<bool> RunSetupAsAdminAsync(bool install)
    {
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = install ? InstallDriverArgument : UninstallDriverArgument,
            Verb = "runas",
            UseShellExecute = true
        };

        try
        {
            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            await process.WaitForExitAsync();
            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("Admin-Prozess konnte nicht gestartet werden", ex);
            return false;
        }
    }
}
