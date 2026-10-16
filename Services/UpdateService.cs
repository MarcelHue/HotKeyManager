using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace HotKeyManager.Services;

/// <summary>
/// Checks GitHub Releases for new versions and applies Velopack updates.
/// Works only in an app installed via Velopack; started from a build folder (development),
/// <see cref="IsSupported"/> is false and every check is a no-op.
/// </summary>
public class UpdateService
{
    private const string RepoUrl = "https://github.com/MarcelHue/HotKeyManager";
    private const string UnknownVersion = "?";

    private readonly UpdateManager updateManager = new(new GithubSource(RepoUrl, accessToken: null, prerelease: false));

    /// <summary>True when the app was installed via Velopack and can update itself.</summary>
    public bool IsSupported => this.updateManager.IsInstalled;

    /// <summary>The running version: the Velopack version if installed, otherwise the assembly version.</summary>
    public string CurrentVersion
    {
        get
        {
            if (this.updateManager.IsInstalled && (this.updateManager.CurrentVersion is not null))
            {
                return this.updateManager.CurrentVersion.ToString();
            }

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null ? UnknownVersion : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    /// <summary>Returns update info when a newer version is available on GitHub, otherwise null.</summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (!this.IsSupported)
        {
            return null;
        }

        try
        {
            return await this.updateManager.CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            // Offline, rate limit or repo unreachable: not an error worth showing to the user.
            System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>Downloads the update and restarts the app into the new version.</summary>
    /// <param name="updateInfo">Result of <see cref="CheckForUpdatesAsync"/>.</param>
    /// <param name="progress">Receives the download progress from 0 to 100.</param>
    public async Task DownloadAndApplyAsync(UpdateInfo updateInfo, Action<int>? progress = null)
    {
        await this.updateManager.DownloadUpdatesAsync(updateInfo, progress);
        this.updateManager.ApplyUpdatesAndRestart(updateInfo);
    }
}
