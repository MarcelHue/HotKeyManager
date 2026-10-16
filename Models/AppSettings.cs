namespace HotKeyManager.Models;

/// <summary>User settings stored in config.json.</summary>
public class AppSettings
{
    public bool RunAtStartup { get; set; }

    public bool MinimizeToTray { get; set; } = true;

    public bool StartMinimized { get; set; }

    /// <summary>"dark", "light" or "system".</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>Name of the minimum <c>LogLevel</c> that is written to the log.</summary>
    public string LogLevel { get; set; } = "Warning";
}
