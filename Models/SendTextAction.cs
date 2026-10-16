namespace HotKeyManager.Models;

/// <summary>Types a text character by character.</summary>
public class SendTextAction : ActionBase
{
    private const int MaxDescriptionLength = 30;
    private const int DefaultCharDelayMs = 5;

    public override ActionType Type => ActionType.SendText;

    public override string DisplayName => "Text senden";

    public override string Description
    {
        get
        {
            if (string.IsNullOrEmpty(this.Text))
            {
                return "(leer)";
            }

            return this.Text.Length > MaxDescriptionLength ? this.Text[..MaxDescriptionLength] + "..." : this.Text;
        }
    }

    public string Text { get; set; } = string.Empty;

    /// <summary>Pause between characters in milliseconds (typing speed).</summary>
    public int CharDelayMs { get; set; } = DefaultCharDelayMs;
}
