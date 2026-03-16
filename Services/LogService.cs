namespace HotKeyManager.Services;

public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3
}

public class LogMessageEventArgs : EventArgs
{
    public LogLevel Level { get; }
    public string Message { get; }

    public LogMessageEventArgs(LogLevel level, string message)
    {
        Level = level;
        Message = message;
    }
}

public class LogService : IDisposable
{
    private static readonly string LogFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HotKeyManager");

    private static readonly string LogPath = Path.Combine(LogFolder, "hotkey-manager.log");
    private static readonly string LogBackupPath = Path.Combine(LogFolder, "hotkey-manager.log.bak");

    private const long MaxLogFileSize = 5 * 1024 * 1024; // 5 MB

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private StreamWriter? _writer;
    private bool _disposed;

    public LogLevel MinLogLevel { get; set; } = LogLevel.Warning;

    public event EventHandler<LogMessageEventArgs>? ErrorOccurred;

    public void Log(LogLevel level, string message, Exception? ex = null)
    {
        if (level < MinLogLevel) return;

        var fullMessage = ex != null ? $"{message}: {ex.Message}" : message;
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToString().ToUpper()}] {fullMessage}";

        _ = WriteLineAsync(line);

        if (level >= LogLevel.Warning)
        {
            ErrorOccurred?.Invoke(this, new LogMessageEventArgs(level, fullMessage));
        }
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message, Exception? ex = null) => Log(LogLevel.Warning, message, ex);
    public void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);

    /// <summary>
    /// Synchroner Schreibvorgang fuer fatale Crashes. Umgeht den async-Writer und schreibt
    /// direkt in die Datei, da bei einem Crash der async-Write nicht mehr abgeschlossen wird.
    /// </summary>
    public void Fatal(string message, Exception? ex = null)
    {
        try
        {
            var fullMessage = ex != null
                ? $"{message}: {ex.Message}\n{ex.StackTrace}"
                : message;
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [FATAL] {fullMessage}";

            Directory.CreateDirectory(LogFolder);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // Absoluter Notfall - nichts mehr zu tun
        }
    }

    private async Task WriteLineAsync(string line)
    {
        try
        {
            await _writeLock.WaitAsync();
            try
            {
                await EnsureWriterAsync();
                if (_writer != null)
                {
                    await _writer.WriteLineAsync(line);
                    await _writer.FlushAsync();
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch
        {
            // Last resort: don't let logging crash the app
        }
    }

    private async Task EnsureWriterAsync()
    {
        Directory.CreateDirectory(LogFolder);

        if (_writer != null)
        {
            try
            {
                var fileInfo = new FileInfo(LogPath);
                if (fileInfo.Exists && fileInfo.Length >= MaxLogFileSize)
                {
                    await _writer.DisposeAsync();
                    _writer = null;
                    RotateLogFile();
                }
                else
                {
                    return;
                }
            }
            catch
            {
                _writer = null;
            }
        }

        _writer = new StreamWriter(LogPath, append: true) { AutoFlush = false };
    }

    private void RotateLogFile()
    {
        try
        {
            if (File.Exists(LogBackupPath))
                File.Delete(LogBackupPath);

            if (File.Exists(LogPath))
                File.Move(LogPath, LogBackupPath);
        }
        catch
        {
            // Rotation failed — continue with the existing file
        }
    }

    public static LogLevel ParseLogLevel(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "debug" => LogLevel.Debug,
            "info" => LogLevel.Info,
            "warning" => LogLevel.Warning,
            "error" => LogLevel.Error,
            _ => LogLevel.Warning
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _writer?.Dispose();
        _writer = null;
        _writeLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    ~LogService() => Dispose();
}
