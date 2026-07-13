namespace HotKeyManager.Models;

/// <summary>
/// Wartet die angegebene Zeit. Vor allem als Baustein innerhalb eines Makros gedacht.
/// </summary>
public class DelayAction : ActionBase
{
    public override ActionType Type => ActionType.Delay;
    public override string DisplayName => "Verzögerung";
    public override string Description => $"{DurationMs} ms warten";

    public int DurationMs { get; set; } = 500;
}
