namespace HotKeyManager.Services;

/// <summary>A DisplayPad key as PadDeck reports it.</summary>
public sealed class PadKeyInfo
{
    public Guid ProfileId { get; set; }

    public Guid PageId { get; set; }

    /// <summary>Key 0-11, row by row from the top left.</summary>
    public int Slot { get; set; }

    public string ProfileName { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    /// <summary>The key has its own PadDeck action, which a claim switches off.</summary>
    public bool HasPadDeckAction { get; set; }

    /// <summary>Owner of an existing claim on this key; null when the key is free.</summary>
    public string? ClaimedBy { get; set; }
}
