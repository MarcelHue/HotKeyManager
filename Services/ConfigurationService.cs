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
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    public AppConfiguration Configuration { get; set; } = new();
    
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = await File.ReadAllTextAsync(ConfigPath);
                Configuration = JsonSerializer.Deserialize<AppConfiguration>(json, JsonOptions) ?? new();
            }
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Fehler beim Laden der Konfiguration", ex);
            Configuration = new AppConfiguration();
        }
    }
    
    public async Task SaveAsync()
    {
        try
        {
            // Ensure directory exists
            Directory.CreateDirectory(ConfigFolder);
            
            var json = JsonSerializer.Serialize(Configuration, JsonOptions);
            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch (Exception ex)
        {
            App.Current.LogService.Error("Fehler beim Speichern der Konfiguration", ex);
        }
    }
    
    public async Task ResetAsync()
    {
        Configuration = new AppConfiguration();
        await SaveAsync();
    }
}
