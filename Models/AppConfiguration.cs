namespace HotKeyManager.Models;

public class AppConfiguration
{
    public List<HotkeyDefinition> Hotkeys { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}

public class AppSettings
{
    public bool RunAtStartup { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool StartMinimized { get; set; } = false;
    public string Theme { get; set; } = "dark";
    public string LogLevel { get; set; } = "Warning";
}
