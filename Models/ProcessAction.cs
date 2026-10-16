namespace HotKeyManager.Models;

/// <summary>Starts a program.</summary>
public class ProcessAction : ActionBase
{
    public override ActionType Type => ActionType.StartProcess;

    public override string DisplayName => "Programm starten";

    public override string Description => Path.GetFileName(this.FilePath);

    public string FilePath { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public bool RunAsAdmin { get; set; }

    public bool Hidden { get; set; }
}
