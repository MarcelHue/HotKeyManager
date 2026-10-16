namespace HotKeyManager.Models;

/// <summary>How a hotkey relates to a target window.</summary>
public enum WindowTargetMode
{
    /// <summary>No window restriction; the hotkey always fires.</summary>
    None,

    /// <summary>Fire only while the target window is active.</summary>
    OnlyWhenActive,

    /// <summary>Send keys to a background window, even when it is not active.</summary>
    SendToBackground,
}
