namespace HotKeyManager.Models;

public class SendTextAction : ActionBase
{
    public override ActionType Type => ActionType.SendText;
    public override string DisplayName => "Text senden";
    public override string Description => string.IsNullOrEmpty(Text) 
        ? "(leer)" 
        : Text.Length > 30 
            ? Text[..30] + "..." 
            : Text;
    
    public string Text { get; set; } = string.Empty;

    /// <summary>Verzoegerung zwischen den Zeichen in Millisekunden (Tippgeschwindigkeit).</summary>
    public int CharDelayMs { get; set; } = 5;
}
