namespace HotKeyManager.Helpers;

/// <summary>Handle, title and owning process of a top-level window.</summary>
public class WindowInfo
{
    public IntPtr Handle { get; set; }

    public string Title { get; set; } = string.Empty;

    public string ProcessName { get; set; } = string.Empty;

    public uint ProcessId { get; set; }

    public override string ToString() => $"{this.ProcessName}: {this.Title}";
}
