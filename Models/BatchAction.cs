namespace HotKeyManager.Models;

public class BatchAction : ActionBase
{
    public override ActionType Type => ActionType.BatchCommand;
    public override string DisplayName => "Batch-Befehl";
    public override string Description => Command.Length > 50 ? Command[..47] + "..." : Command;
    
    public string Command { get; set; } = string.Empty;
    public bool RunHidden { get; set; } = true;
    public bool WaitForExit { get; set; } = false;
    public string WorkingDirectory { get; set; } = string.Empty;
}
