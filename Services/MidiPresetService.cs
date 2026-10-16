using System.Text.Json;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

/// <summary>
/// Loads MIDI command presets from JSON files: bundled ones from the app folder,
/// user presets from %APPDATA%\HotKeyManager\Presets\midi.
/// </summary>
public class MidiPresetService
{
    private const string PresetSearchPattern = "*.json";
    private const string PresetFileExtension = ".json";
    private const string FallbackFileName = "preset";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // Relaxed escaping keeps umlauts readable in hand-edited preset files.
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Folder for user presets; also the target of saves and imports.</summary>
    public static string UserFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HotKeyManager", "Presets", "midi");

    private static string BuiltInFolder => Path.Combine(AppContext.BaseDirectory, "Presets", "midi");

    /// <summary>All available presets; a user preset replaces a bundled one with the same name.</summary>
    public IReadOnlyList<MidiPreset> LoadAll()
    {
        var presets = new List<MidiPreset>();

        foreach (var folder in new[] { BuiltInFolder, UserFolder })
        {
            if (!Directory.Exists(folder))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(folder, PresetSearchPattern))
            {
                var preset = this.Load(file);
                if (preset is not null)
                {
                    AddOrReplace(presets, preset);
                }
            }
        }

        return presets;
    }

    /// <summary>Loads one preset file; returns null (and logs) when it is invalid or has no name.</summary>
    public MidiPreset? Load(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var preset = JsonSerializer.Deserialize<MidiPreset>(json, JsonOptions);
            if ((preset is null) || string.IsNullOrWhiteSpace(preset.Name))
            {
                return null;
            }

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
    /// Writes a preset to the user folder. A bundled preset thereby becomes a user copy;
    /// the program folder stays untouched, as updates overwrite it anyway.
    /// </summary>
    public bool Save(MidiPreset preset)
    {
        try
        {
            Directory.CreateDirectory(UserFolder);

            var fileName = !string.IsNullOrEmpty(preset.FilePath)
                ? Path.GetFileName(preset.FilePath)
                : MakeFileName(preset.Name);

            var target = Path.Combine(UserFolder, fileName);

            var json = JsonSerializer.Serialize(preset, WriteOptions);

            File.WriteAllText(target, json);
            preset.FilePath = target;

            App.Current.LogService.Info($"MIDI-Preset gespeichert: {preset.Name}");
            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("MIDI-Preset konnte nicht gespeichert werden", ex);
            return false;
        }
    }

    /// <summary>Copies a preset file into the user folder.</summary>
    /// <returns>The loaded preset, or null if the file is not a valid preset.</returns>
    public MidiPreset? Import(string sourcePath)
    {
        try
        {
            // Validate before copying, so an invalid file never lands in the user folder.
            var parsed = this.Load(sourcePath);
            if (parsed is null)
            {
                return null;
            }

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

    private static void AddOrReplace(List<MidiPreset> presets, MidiPreset preset)
    {
        var existing = presets.FindIndex(p => string.Equals(p.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            presets[existing] = preset;
        }
        else
        {
            presets.Add(preset);
        }
    }

    private static string MakeFileName(string presetName)
    {
        var safeChars = presetName.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray();
        var safe = new string(safeChars);

        while (safe.Contains("--"))
        {
            safe = safe.Replace("--", "-");
        }

        safe = safe.Trim('-');

        return (safe.Length == 0 ? FallbackFileName : safe) + PresetFileExtension;
    }
}
