namespace HotKeyManager.Services;

/// <summary>
/// Writes the app log to %APPDATA%\HotKeyManager and raises <see cref="ErrorOccurred"/> for warnings and errors.
/// </summary>
public class LogService : IDisposable
{
    private const long MaxLogFileSize = 5 * 1024 * 1024;

    private static readonly string LogFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HotKeyManager");

    private static readonly string LogPath = Path.Combine(LogFolder, "hotkey-manager.log");
    private static readonly string LogBackupPath = Path.Combine(LogFolder, "hotkey-manager.log.bak");

    private readonly SemaphoreSlim writeLock = new(1, 1);
    private StreamWriter? writer;
    private bool disposed;

    ~LogService() => this.Dispose();

    /// <summary>Raised for every entry of level Warning or higher; may come from any thread.</summary>
    public event EventHandler<LogMessageEventArgs>? ErrorOccurred;

    /// <summary>Entries below this level are dropped.</summary>
    public LogLevel MinLogLevel { get; set; } = LogLevel.Warning;

    /// <summary>Parses a level name from the settings; unknown values fall back to Warning.</summary>
    public static LogLevel ParseLogLevel(string? value) => value?.ToLowerInvariant() switch
    {
        "debug" => LogLevel.Debug,
        "info" => LogLevel.Info,
        "warning" => LogLevel.Warning,
        "error" => LogLevel.Error,
        _ => LogLevel.Warning
    };

    /// <summary>Writes an entry in the background if it reaches <see cref="MinLogLevel"/>.</summary>
    public void Log(LogLevel level, string message, Exception? ex = null)
    {
        if (level < this.MinLogLevel)
        {
            return;
        }

        var fullMessage = ex is not null ? $"{message}: {ex.Message}" : message;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToString().ToUpper()}] {fullMessage}";

        _ = this.WriteLineAsync(line);

        if (level >= LogLevel.Warning)
        {
            this.ErrorOccurred?.Invoke(this, new LogMessageEventArgs(level, fullMessage));
        }
    }

    /// <summary>Logs at level Debug.</summary>
    public void Debug(string message) => this.Log(LogLevel.Debug, message);

    /// <summary>Logs at level Info.</summary>
    public void Info(string message) => this.Log(LogLevel.Info, message);

    /// <summary>Logs at level Warning.</summary>
    public void Warning(string message, Exception? ex = null) => this.Log(LogLevel.Warning, message, ex);

    /// <summary>Logs at level Error.</summary>
    public void Error(string message, Exception? ex = null) => this.Log(LogLevel.Error, message, ex);

    /// <summary>
    /// Writes synchronously, bypassing the async writer, because during a fatal crash
    /// an async write would not complete anymore.
    /// </summary>
    public void Fatal(string message, Exception? ex = null)
    {
        try
        {
            var fullMessage = ex is not null ? $"{message}: {ex.Message}\n{ex.StackTrace}" : message;
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FATAL] {fullMessage}";

            Directory.CreateDirectory(LogFolder);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch (Exception)
        {
            // The app is already crashing and the log itself failed; there is nowhere left to report it.
        }
    }

    /// <summary>Closes the log file.</summary>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.writer?.Dispose();
        this.writer = null;
        this.writeLock.Dispose();
        this.disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task WriteLineAsync(string line)
    {
        try
        {
            await this.writeLock.WaitAsync();
            try
            {
                await this.EnsureWriterAsync();
                if (this.writer is not null)
                {
                    await this.writer.WriteLineAsync(line);
                    await this.writer.FlushAsync();
                }
            }
            finally
            {
                this.writeLock.Release();
            }
        }
        catch (Exception)
        {
            // A failing logger must not crash the app, and it cannot log its own failure.
        }
    }

    private async Task EnsureWriterAsync()
    {
        Directory.CreateDirectory(LogFolder);

        if (this.writer is not null)
        {
            try
            {
                var fileInfo = new FileInfo(LogPath);
                if (!fileInfo.Exists || (fileInfo.Length < MaxLogFileSize))
                {
                    return;
                }

                await this.writer.DisposeAsync();
                this.writer = null;
                RotateLogFile();
            }
            catch (Exception)
            {
                // Open a fresh writer below; the logger cannot log its own failure.
                this.writer = null;
            }
        }

        this.writer = new StreamWriter(LogPath, append: true) { AutoFlush = false };
    }

    private static void RotateLogFile()
    {
        try
        {
            if (File.Exists(LogBackupPath))
            {
                File.Delete(LogBackupPath);
            }

            if (File.Exists(LogPath))
            {
                File.Move(LogPath, LogBackupPath);
            }
        }
        catch (Exception)
        {
            // Keep writing to the existing file; the logger cannot log its own failure.
        }
    }
}
