using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>
/// Documented MIDI commands for one device, loaded from JSON.
/// Bundled presets live in the app folder under "Presets\midi",
/// user presets under %APPDATA%\HotKeyManager\Presets\midi.
/// </summary>
public class MidiPreset
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Prerequisites on the device, e.g. that MIDI control must be enabled.</summary>
    public string? Notes { get; set; }

    public List<MidiPresetCommand> Commands { get; set; } = new();

    /// <summary>File the preset was loaded from; not serialized.</summary>
    [JsonIgnore]
    public string? FilePath { get; set; }
}
