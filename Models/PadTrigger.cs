using System.Text.Json.Serialization;

namespace HotKeyManager.Models;

/// <summary>
/// DisplayPad trigger via PadDeck: one key on one page of a PadDeck profile.
/// Captured with "Taste lernen"; PadDeck then switches off its own action for that key.
/// </summary>
public class PadTrigger
{
    public Guid ProfileId { get; set; }

    public Guid PageId { get; set; }

    /// <summary>Key 0-11, row by row from the top left (shown as 1-12).</summary>
    public int Slot { get; set; }

    /// <summary>Profile name at learning time, for display only.</summary>
    public string ProfileName { get; set; } = string.Empty;

    /// <summary>Page name at learning time, for display only.</summary>
    public string PageName { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayText => $"{this.ProfileName} › {this.PageName} › Taste {this.Slot + 1}";

    /// <summary>True when this trigger is the given key.</summary>
    public bool Matches(Guid profileId, Guid pageId, int slot) =>
        this.ProfileId == profileId && this.PageId == pageId && this.Slot == slot;
}
