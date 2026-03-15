using Microsoft.Win32;

namespace HotKeyManager.Services;

public class AutoStartService
{
    private const string AppName = "HotKeyManager";
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    
    public bool IsAutoStartEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                var value = key?.GetValue(AppName);
                return value != null;
            }
            catch
            {
                return false;
            }
        }
    }
    
    public bool SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            
            if (key == null) return false;
            
            if (enabled)
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(AppName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
            
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"AutoStart error: {ex.Message}");
            return false;
        }
    }
}
