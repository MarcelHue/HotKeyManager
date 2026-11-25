namespace HotKeyManager.Models;

/// <summary>Sends a sequence of key strokes.</summary>
public class KeySequenceAction : ActionBase
{
    public override ActionType Type => ActionType.KeySequence;

    public override string DisplayName => "Tastensequenz";

    public override string Description => $"{this.Keys.Count} Tasten";

    public List<KeyStroke> Keys { get; set; } = new();
}
