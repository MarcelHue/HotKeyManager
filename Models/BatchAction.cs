namespace HotKeyManager.Models;

/// <summary>Runs a command line through cmd.exe.</summary>
public class BatchAction : ActionBase
{
    private const int MaxDescriptionLength = 50;
    private const string Ellipsis = "...";

    public override ActionType Type => ActionType.BatchCommand;

    public override string DisplayName => "Batch-Befehl";

    public override string Description => this.Command.Length > MaxDescriptionLength
        ? this.Command[..(MaxDescriptionLength - Ellipsis.Length)] + Ellipsis
        : this.Command;

    public string Command { get; set; } = string.Empty;

    public bool RunHidden { get; set; } = true;

    public bool WaitForExit { get; set; }

    public string WorkingDirectory { get; set; } = string.Empty;
}
