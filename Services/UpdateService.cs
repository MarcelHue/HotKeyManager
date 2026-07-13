using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace HotKeyManager.Services;

/// <summary>
/// Prueft GitHub Releases auf neue Versionen und fuehrt Velopack-Updates durch.
/// Funktioniert nur in einer per Velopack installierten App — beim Start aus einem
/// Build-Verzeichnis (Entwicklung) ist IsSupported false und alle Checks sind No-Ops.
/// </summary>
public class UpdateService
{
    private const string RepoUrl = "https://github.com/MarcelHue/HotKeyManager";

    private readonly UpdateManager _updateManager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));

    /// <summary>Update-Checks sind nur in der installierten App moeglich.</summary>
    public bool IsSupported => _updateManager.IsInstalled;

    /// <summary>Aktuell laufende Version (Velopack-Version, sonst Assembly-Version).</summary>
    public string CurrentVersion
    {
        get
        {
            if (_updateManager.IsInstalled && _updateManager.CurrentVersion != null)
                return _updateManager.CurrentVersion.ToString();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version == null ? "?" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>Liefert Update-Infos, wenn eine neuere Version auf GitHub verfuegbar ist, sonst null.</summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!IsSupported) return null;

        try
        {
            return await _updateManager.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            // Offline / Rate-Limit / Repo nicht erreichbar: kein Fehler fuer den User
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Laedt das Update herunter (progress: 0-100) und startet die App neu.</summary>
    public async Task DownloadAndApplyAsync(UpdateInfo updateInfo, Action<int>? progress = null)
    {
        await _updateManager.DownloadUpdatesAsync(updateInfo, progress);
        _updateManager.ApplyUpdatesAndRestart(updateInfo);
    }
}
