using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace HotKeyManager.Helpers;

/// <summary>Finds and matches top-level windows and posts keys to them.</summary>
public static class WindowHelper
{
    private const int ScanCodeShift = 16;
    private const uint SingleRepeatCount = 1;
    private const uint PreviousKeyStateFlag = 1u << 30;
    private const uint TransitionStateFlag = 1u << 31;

    /// <summary>Checks whether the foreground window matches the criteria.</summary>
    /// <param name="processName">Process name without .exe, or null for any.</param>
    /// <param name="titlePattern">Title pattern with * wildcards, or null for any.</param>
    public static bool IsActiveWindowMatch(string? processName, string? titlePattern)
    {
        var hwnd = WindowNative.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        return IsWindowMatch(hwnd, processName, titlePattern);
    }

    /// <summary>Finds the first visible window that matches process name and title pattern.</summary>
    /// <param name="processName">Process name without .exe, or null for any.</param>
    /// <param name="titlePattern">Title pattern with * wildcards, or null for any.</param>
    /// <returns>The window handle, or <see cref="IntPtr.Zero"/> when none matches.</returns>
    public static IntPtr FindWindowByProcessAndTitle(string? processName, string? titlePattern)
    {
        var foundWindow = IntPtr.Zero;

        // Returning false from the callback stops the enumeration.
        WindowNative.EnumWindows(
            (hwnd, _) =>
            {
                if (!WindowNative.IsWindowVisible(hwnd) || !IsWindowMatch(hwnd, processName, titlePattern))
                {
                    return true;
                }

                foundWindow = hwnd;
                return false;
            },
            IntPtr.Zero);

        return foundWindow;
    }

    /// <summary>Finds all visible windows that match process name and title pattern.</summary>
    public static List<WindowInfo> FindAllMatchingWindows(string? processName, string? titlePattern)
    {
        var windows = new List<WindowInfo>();

        WindowNative.EnumWindows(
            (hwnd, _) =>
            {
                if (!WindowNative.IsWindowVisible(hwnd) || !IsWindowMatch(hwnd, processName, titlePattern))
                {
                    return true;
                }

                var info = GetWindowInfo(hwnd);
                if (info is not null)
                {
                    windows.Add(info);
                }

                return true;
            },
            IntPtr.Zero);

        return windows;
    }

    /// <summary>Posts a key message to a window, which also works while it is in the background.</summary>
    /// <param name="hwnd">Target window.</param>
    /// <param name="virtualKeyCode">Virtual key code of the key.</param>
    /// <param name="isKeyDown">True for key down, false for key up.</param>
    /// <returns>True when the message was posted.</returns>
    public static bool SendKeyToWindow(IntPtr hwnd, int virtualKeyCode, bool isKeyDown)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var msg = isKeyDown ? WindowNative.WM_KEYDOWN : WindowNative.WM_KEYUP;

        // lParam layout: bits 0-15 repeat count, 16-23 scan code; for key up also bit 30 (previous
        // state) and bit 31 (transition). Some apps read the scan code instead of the virtual key.
        var scanCode = WindowNative.MapVirtualKey((uint)virtualKeyCode, WindowNative.MAPVK_VK_TO_VSC);
        var lParamValue = SingleRepeatCount | (scanCode << ScanCodeShift);
        if (!isKeyDown)
        {
            lParamValue |= PreviousKeyStateFlag | TransitionStateFlag;
        }

        return WindowNative.PostMessage(hwnd, msg, (IntPtr)virtualKeyCode, (IntPtr)lParamValue);
    }

    /// <summary>Returns information about the foreground window, or null if there is none.</summary>
    public static WindowInfo? GetActiveWindowInfo()
    {
        var hwnd = WindowNative.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        return GetWindowInfo(hwnd);
    }

    /// <summary>Returns information about a window, or null if it cannot be read.</summary>
    public static WindowInfo? GetWindowInfo(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            WindowNative.GetWindowThreadProcessId(hwnd, out var processId);
            return new WindowInfo
            {
                Handle = hwnd,
                Title = GetWindowTitle(hwnd),
                ProcessName = Process.GetProcessById((int)processId).ProcessName,
                ProcessId = processId
            };
        }
        catch (Exception)
        {
            // The owning process may have exited since the handle was obtained; callers treat
            // null as "no such window", and this also runs per window during enumeration.
            return null;
        }
    }

    /// <summary>Checks a text against a pattern where * matches any characters; case-insensitive.</summary>
    /// <returns>True when the pattern is empty or matches the whole text.</returns>
    public static bool WildcardMatch(string? text, string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true;
        }

        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsWindowMatch(IntPtr hwnd, string? processName, string? titlePattern)
    {
        try
        {
            if (!string.IsNullOrEmpty(processName))
            {
                WindowNative.GetWindowThreadProcessId(hwnd, out var processId);
                var process = Process.GetProcessById((int)processId);
                if (!string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return string.IsNullOrEmpty(titlePattern) || WildcardMatch(GetWindowTitle(hwnd), titlePattern);
        }
        catch (Exception)
        {
            // Called for every window during enumeration; a window whose process has already exited
            // simply does not match, and logging it would flood the log.
            return false;
        }
    }

    private static string GetWindowTitle(IntPtr hwnd)
    {
        var length = WindowNative.GetWindowTextLength(hwnd);
        var buffer = new StringBuilder(length + 1);
        WindowNative.GetWindowText(hwnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
