namespace HotKeyManager.Models;

/// <summary>Root object of config.json: all hotkeys plus the app settings.</summary>
public class AppConfiguration
{
    public List<HotkeyDefinition> Hotkeys { get; set; } = new();

    public AppSettings Settings { get; set; } = new();
}
