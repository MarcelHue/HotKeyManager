using Microsoft.Win32;

namespace HotKeyManager.Services;

/// <summary>Registers the app in the current user's Run key so it starts with Windows.</summary>
public class AutoStartService
{
    private const string AppName = "HotKeyManager";
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>True when the Run key contains an entry for the app.</summary>
    public bool IsAutoStartEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                return key?.GetValue(AppName) is not null;
            }
            catch (Exception ex)
            {
                App.Current.LogService.Warning("AutoStart-Status konnte nicht gelesen werden", ex);
                return false;
            }
        }
    }

    /// <summary>Adds or removes the Run key entry for the current executable.</summary>
    /// <returns>False when the Run key could not be opened or written.</returns>
    public bool SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            if (key is null)
            {
                return false;
            }

            if (!enabled)
            {
                key.DeleteValue(AppName, false);
                return true;
            }

            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                key.SetValue(AppName, $"\"{exePath}\"");
            }

            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning("AutoStart-Einstellung konnte nicht gespeichert werden", ex);
            return false;
        }
    }
}
