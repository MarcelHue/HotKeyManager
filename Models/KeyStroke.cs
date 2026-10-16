using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>One entry of a key sequence: a key with modifiers and the pause after it.</summary>
public class KeyStroke
{
    private const int DefaultDelayAfterMs = 50;

    public int VirtualKeyCode { get; set; }

    public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;

    public int DelayAfterMs { get; set; } = DefaultDelayAfterMs;

    public string DisplayText { get; set; } = string.Empty;

    public KeyEventType EventType { get; set; } = KeyEventType.KeyPress;

    /// <summary>Display text with an arrow for down/up events, used in advanced mode.</summary>
    [JsonIgnore]
    public string DisplayTextWithType => this.EventType switch
    {
        KeyEventType.KeyDown => $"↓ {this.DisplayText}",
        KeyEventType.KeyUp => $"↑ {this.DisplayText}",
        _ => this.DisplayText
    };
}
