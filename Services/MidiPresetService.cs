using System.Text.Json;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

/// <summary>
/// Laedt MIDI-Befehls-Presets aus JSON-Dateien:
/// mitgelieferte aus dem App-Ordner, eigene aus %APPDATA%\HotKeyManager\Presets\midi.
/// </summary>
public class MidiPresetService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static string BuiltInFolder => Path.Combine(AppContext.BaseDirectory, "Presets", "midi");

    public static string UserFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HotKeyManager", "Presets", "midi");

    /// <summary>
    /// Alle verfuegbaren Presets. Eigene Presets mit gleichem Namen
    /// ueberschreiben mitgelieferte.
    /// </summary>
    public IReadOnlyList<MidiPreset> LoadAll()
    {
        var presets = new List<MidiPreset>();

        foreach (var folder in new[] { BuiltInFolder, UserFolder })
        {
            if (!Directory.Exists(folder)) continue;

            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                var preset = Load(file);
                if (preset == null) continue;

                var existing = presets.FindIndex(p =>
                    string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));

                if (existing >= 0) presets[existing] = preset;
                else presets.Add(preset);
            }
        }

        return presets;
    }

    public MidiPreset? Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var preset = JsonSerializer.Deserialize<MidiPreset>(json, JsonOptions);
            if (preset == null || string.IsNullOrWhiteSpace(preset.Name)) return null;

            preset.FilePath = path;
            return preset;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Warning($"MIDI-Preset konnte nicht geladen werden: {Path.GetFileName(path)}", ex);
            return null;
        }
    }

    /// <summary>
    /// Kopiert eine Preset-Datei in den Benutzer-Ordner. Liefert das geladene Preset
    /// oder null, wenn die Datei kein gueltiges Preset ist.
    /// </summary>
    public MidiPreset? Import(string sourcePath)
    {
        try
        {
            // Erst validieren, dann erst kopieren
            var parsed = Load(sourcePath);
            if (parsed == null) return null;

            Directory.CreateDirectory(UserFolder);
            var target = Path.Combine(UserFolder, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, target, overwrite: true);

            parsed.FilePath = target;
            App.Current.LogService.Info($"MIDI-Preset importiert: {parsed.Name}");
            return parsed;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("MIDI-Preset konnte nicht importiert werden", ex);
            return null;
        }
    }
}
