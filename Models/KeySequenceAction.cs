namespace HotKeyManager.Models;

public class KeySequenceAction : ActionBase
{
    public override ActionType Type => ActionType.KeySequence;
    public override string DisplayName => "Tastensequenz";
    public override string Description => $"{Keys.Count} Tasten";

    public List<KeyStroke> Keys { get; set; } = new();

    /// <summary>
    /// Wenn true, werden Tasten über den Kernel-Mode Treiber injiziert (kein LLKHF_INJECTED-Flag).
    /// Notwendig für Apps die synthetische Tastatureingaben filtern (z.B. via keyboard-Library).
    /// </summary>
    public bool UseKernelInjection { get; set; } = false;
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
