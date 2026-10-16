namespace HotKeyManager.Services;

/// <summary>A warning or error that was written to the log, for showing it in the UI.</summary>
public class LogMessageEventArgs : EventArgs
{
    /// <summary>Creates the event data for one log entry.</summary>
    public LogMessageEventArgs(LogLevel level, string message)
    {
        this.Level = level;
        this.Message = message;
    }

    public LogLevel Level { get; }

    /// <summary>The message including the exception message, if any.</summary>
    public string Message { get; }
}
