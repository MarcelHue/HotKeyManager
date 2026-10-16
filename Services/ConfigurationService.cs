using System.Text.Json;
using System.Text.Json.Serialization;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

/// <summary>Loads and saves config.json in %APPDATA%\HotKeyManager, with a backup of the previous version.</summary>
public class ConfigurationService
{
    private static readonly string ConfigFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HotKeyManager");

    private static readonly string ConfigPath = Path.Combine(ConfigFolder, "config.json");
    private static readonly string ConfigBackupPath = Path.Combine(ConfigFolder, "config.json.bak");
    private static readonly string ConfigTempPath = ConfigPath + ".tmp";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // SaveAsync is called fire-and-forget in many places; without the lock parallel saves
    // would fight over the temp file.
    private readonly SemaphoreSlim saveLock = new(1, 1);

    /// <summary>The current configuration; changes are written by <see cref="SaveAsync"/>.</summary>
    public AppConfiguration Configuration { get; set; } = new();

    /// <summary>Loads config.json, falls back to the backup, and starts empty if both fail.</summary>
    public async Task LoadAsync()
    {
        if (await this.TryLoadFromAsync(ConfigPath))
        {
            return;
        }

        // The main file can be corrupt after an interrupted save; the backup is the previous version.
        if (await this.TryLoadFromAsync(ConfigBackupPath))
        {
            App.Current.LogService.Warning("config.json war defekt — Backup wurde wiederhergestellt.");
            await this.SaveAsync();
            return;
        }

        this.Configuration = new AppConfiguration();
    }

    /// <summary>Writes the configuration atomically; errors are logged, not thrown.</summary>
    public async Task SaveAsync()
    {
        await this.saveLock.WaitAsync();
        try
        {
            Directory.CreateDirectory(ConfigFolder);

            var json = JsonSerializer.Serialize(this.Configuration, JsonOptions);

            // Write to a temp file first and then swap it in, keeping the old file as backup.
            // A crash in the middle of saving then always leaves a valid config behind.
            await File.WriteAllTextAsync(ConfigTempPath, json);

            if (File.Exists(ConfigPath))
            {
                File.Replace(ConfigTempPath, ConfigPath, ConfigBackupPath);
            }
            else
            {
                File.Move(ConfigTempPath, ConfigPath);
            }
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Fehler beim Speichern der Konfiguration", ex);
        }
        finally
        {
            this.saveLock.Release();
        }
    }

    /// <summary>Replaces the configuration with defaults and saves it.</summary>
    public async Task ResetAsync()
    {
        this.Configuration = new AppConfiguration();
        await this.SaveAsync();
    }

    private async Task<bool> TryLoadFromAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var json = await File.ReadAllTextAsync(path);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions);
            if (config is null)
            {
                return false;
            }

            this.Configuration = config;
            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error($"Fehler beim Laden der Konfiguration ({Path.GetFileName(path)})", ex);
            return false;
        }
    }
}
