using System.Text.Json;
using System.Text.Json.Serialization;
using HotKeyManager.Models;

namespace HotKeyManager.Services;

public class ConfigurationService
{
    private static readonly string ConfigFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HotKeyManager");
    
    private static readonly string ConfigPath = Path.Combine(ConfigFolder, "config.json");
    private static readonly string ConfigBackupPath = Path.Combine(ConfigFolder, "config.json.bak");
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    // SaveAsync wird an vielen Stellen fire-and-forget aufgerufen — der Lock verhindert,
    // dass parallele Saves sich die Temp-Datei gegenseitig wegschnappen.
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public AppConfiguration Configuration { get; set; } = new();
    
    public async Task LoadAsync()
    {
        // Erst die Hauptdatei versuchen, bei Fehler (korrupt/abgebrochener Save)
        // automatisch auf das Backup zurueckfallen.
        if (await TryLoadFromAsync(ConfigPath))
            return;

        if (await TryLoadFromAsync(ConfigBackupPath))
        {
            App.Current.LogService.Warning("config.json war defekt — Backup wurde wiederhergestellt.");
            await SaveAsync();
            return;
        }

        Configuration = new AppConfiguration();
    }

    private async Task<bool> TryLoadFromAsync(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;

            var json = await File.ReadAllTextAsync(path);
            var config = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions);
            if (config == null) return false;

            Configuration = config;
            return true;
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error($"Fehler beim Laden der Konfiguration ({Path.GetFileName(path)})", ex);
            return false;
        }
    }

    public async Task SaveAsync()
    {
        await _saveLock.WaitAsync();
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(ConfigFolder);

            var json = JsonSerializer.Serialize(Configuration, JsonOptions);

            // Atomar schreiben: erst Temp-Datei, dann Replace mit Backup der alten Datei.
            // So bleibt bei einem Absturz mitten im Speichern immer eine gueltige Config erhalten.
            var tempPath = ConfigPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);

            if (File.Exists(ConfigPath))
                File.Replace(tempPath, ConfigPath, ConfigBackupPath);
            else
                File.Move(tempPath, ConfigPath);
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Fehler beim Speichern der Konfiguration", ex);
        }
        finally
        {
            _saveLock.Release();
        }
    }
    
    public async Task ResetAsync()
    {
        Configuration = new AppConfiguration();
        await SaveAsync();
    }
}
