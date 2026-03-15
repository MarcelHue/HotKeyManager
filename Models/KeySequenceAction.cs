namespace HotKeyManager.Models;

public class KeySequenceAction : ActionBase
{
    public override ActionType Type => ActionType.KeySequence;
    public override string DisplayName => "Tastensequenz";
    public override string Description => $"{Keys.Count} Tasten";
    
    public List<KeyStroke> Keys { get; set; } = new();
}

public class KeyStroke
{
    public int VirtualKeyCode { get; set; }
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;
    public int DelayAfterMs { get; set; } = 50;
    public string DisplayText { get; set; } = string.Empty;
    public KeyEventType EventType { get; set; } = KeyEventType.KeyPress;
    
    /// <summary>
    /// Gibt den Anzeigenamen mit EventType-Symbol zurück (für Advanced Mode)
    /// </summary>
    public string DisplayTextWithType => EventType switch
    {
        KeyEventType.KeyDown => $"↓ {DisplayText}",
        KeyEventType.KeyUp => $"↑ {DisplayText}",
        _ => DisplayText
    };
}
