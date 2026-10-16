namespace HotKeyManager.Services;

/// <summary>A pad key reserved by a HotKeyManager hotkey; PadDeck greys out its own action for it.</summary>
public sealed class PadClaim
{
    public Guid ProfileId { get; set; }

    public Guid PageId { get; set; }

    public int Slot { get; set; }

    /// <summary>Shown in PadDeck next to the claimed key (the hotkey name).</summary>
    public string? Label { get; set; }

    /// <summary>The hotkey id, opaque to PadDeck.</summary>
    public string? ExternalId { get; set; }
}
