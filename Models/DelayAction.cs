namespace HotKeyManager.Models;

/// <summary>Waits for a given time; meant mainly as a step inside a macro.</summary>
public class DelayAction : ActionBase
{
    private const int DefaultDurationMs = 500;

    public override ActionType Type => ActionType.Delay;

    public override string DisplayName => "Verzögerung";

    public override string Description => $"{this.DurationMs} ms warten";

    public int DurationMs { get; set; } = DefaultDurationMs;
}
