namespace HotKeyManager.Models;

/// <summary>The Windows system actions a <see cref="SystemAction"/> can run.</summary>
public enum SystemActionKind
{
    PlayPause,
    NextTrack,
    PreviousTrack,
    StopMedia,
    VolumeUp,
    VolumeDown,
    ToggleSystemMute,
    ToggleMicrophoneMute,
    MicrophoneMuteOn,
    MicrophoneMuteOff
}
