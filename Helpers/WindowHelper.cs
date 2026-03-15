using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HotKeyManager.Helpers;

/// <summary>
/// Hilfsmethoden fuer Fenster-Operationen mittels Win32 APIs.
/// </summary>
public static class WindowHelper
{
    #region Win32 API Declarations

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    // Window Messages
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint WM_CHAR = 0x0102;

    #endregion

    #region Public Methods

    /// <summary>
    /// Prueft ob das aktuell aktive Fenster den Kriterien entspricht.
    /// </summary>
    /// <param name="processName">Prozessname (ohne .exe) oder null fuer beliebig.</param>
    /// <param name="titlePattern">Fenstertitel-Pattern mit Wildcard (*) oder null fuer beliebig.</param>
    /// <returns>True wenn das aktive Fenster passt.</returns>
    public static bool IsActiveWindowMatch(string? processName, string? titlePattern)
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return false;

        return IsWindowMatch(hwnd, processName, titlePattern);
    }

    /// <summary>
    /// Findet ein Fenster anhand von Prozessname und/oder Titel-Pattern.
    /// </summary>
    /// <param name="processName">Prozessname (ohne .exe) oder null fuer beliebig.</param>
    /// <param name="titlePattern">Fenstertitel-Pattern mit Wildcard (*) oder null fuer beliebig.</param>
    /// <returns>Das Window-Handle oder IntPtr.Zero wenn nicht gefunden.</returns>
    public static IntPtr FindWindowByProcessAndTitle(string? processName, string? titlePattern)
    {
        IntPtr foundWindow = IntPtr.Zero;

        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd))
                return true; // Weitersuchen

            if (IsWindowMatch(hwnd, processName, titlePattern))
            {
                foundWindow = hwnd;
                return false; // Gefunden, Suche beenden
            }

            return true; // Weitersuchen
        }, IntPtr.Zero);

        return foundWindow;
    }

    /// <summary>
    /// Findet alle Fenster die den Kriterien entsprechen.
    /// </summary>
    public static List<WindowInfo> FindAllMatchingWindows(string? processName, string? titlePattern)
    {
        var windows = new List<WindowInfo>();

        EnumWindows((hwnd, lParam) =>
        {
            if (!IsWindowVisible(hwnd))
                return true;

            if (IsWindowMatch(hwnd, processName, titlePattern))
            {
                var info = GetWindowInfo(hwnd);
                if (info != null)
                    windows.Add(info);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }

    /// <summary>
    /// Sendet eine Taste an ein Fenster (auch im Hintergrund).
    /// </summary>
    /// <param name="hwnd">Das Fenster-Handle.</param>
    /// <param name="virtualKeyCode">Virtual Key Code der Taste.</param>
    /// <param name="isKeyDown">True fuer KeyDown, False fuer KeyUp.</param>
    /// <returns>True wenn erfolgreich gesendet.</returns>
    public static bool SendKeyToWindow(IntPtr hwnd, int virtualKeyCode, bool isKeyDown)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        uint msg = isKeyDown ? WM_KEYDOWN : WM_KEYUP;
        
        // lParam enthaelt Scan-Code und Flags
        // Bit 0-15: Repeat count (1)
        // Bit 16-23: Scan code
        // Bit 24: Extended key flag
        // Bit 29: Context code (0 for WM_KEYDOWN)
        // Bit 30: Previous key state (0 for KeyDown, 1 for KeyUp)
        // Bit 31: Transition state (0 for KeyDown, 1 for KeyUp)
        uint scanCode = MapVirtualKey((uint)virtualKeyCode, 0);
        uint lParamValue = 1; // Repeat count
        lParamValue |= (scanCode << 16);
        
        if (!isKeyDown)
        {
            lParamValue |= (1u << 30); // Previous key state
            lParamValue |= (1u << 31); // Transition state
        }

        return PostMessage(hwnd, msg, (IntPtr)virtualKeyCode, (IntPtr)lParamValue);
    }

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    /// <summary>
    /// Holt Informationen zum aktuell aktiven Fenster.
    /// </summary>
    public static WindowInfo? GetActiveWindowInfo()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        return GetWindowInfo(hwnd);
    }

    /// <summary>
    /// Holt Informationen zu einem Fenster.
    /// </summary>
    public static WindowInfo? GetWindowInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return null;

        try
        {
            // Titel holen
            int length = GetWindowTextLength(hwnd);
            var sb = new StringBuilder(length + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            string title = sb.ToString();

            // Prozess holen
            GetWindowThreadProcessId(hwnd, out uint processId);
            var process = Process.GetProcessById((int)processId);
            string processName = process.ProcessName;

            return new WindowInfo
            {
                Handle = hwnd,
                Title = title,
                ProcessName = processName,
                ProcessId = processId
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Prueft ob ein Text einem Wildcard-Pattern entspricht.
    /// Unterstuetzt * als Wildcard fuer beliebige Zeichen.
    /// </summary>
    public static bool WildcardMatch(string? text, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return true; // Kein Pattern = alles passt

        if (string.IsNullOrEmpty(text))
            return false;

        // Pattern in Regex umwandeln
        // * -> .*
        // Alle anderen Regex-Sonderzeichen escapen
        string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }

    #endregion

    #region Private Methods

    private static bool IsWindowMatch(IntPtr hwnd, string? processName, string? titlePattern)
    {
        try
        {
            // Prozessname pruefen
            if (!string.IsNullOrEmpty(processName))
            {
                GetWindowThreadProcessId(hwnd, out uint processId);
                var process = Process.GetProcessById((int)processId);
                
                if (!string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Titel pruefen
            if (!string.IsNullOrEmpty(titlePattern))
            {
                int length = GetWindowTextLength(hwnd);
                var sb = new StringBuilder(length + 1);
                GetWindowText(hwnd, sb, sb.Capacity);
                string title = sb.ToString();

                if (!WildcardMatch(title, titlePattern))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}

/// <summary>
/// Informationen ueber ein Fenster.
/// </summary>
public class WindowInfo
{
    public IntPtr Handle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public uint ProcessId { get; set; }

    public override string ToString() => $"{ProcessName}: {Title}";
}
