namespace HotKeyManager.Models;

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

/// <summary>
/// Windows-Systemaktion: Medientasten und Mikrofon-Stummschaltung.
/// </summary>
public class SystemAction : ActionBase
{
    public override ActionType Type => ActionType.System;
    public override string DisplayName => "Windows-Aktion";
    public override string Description => Kind switch
    {
        SystemActionKind.PlayPause => "Wiedergabe/Pause",
        SystemActionKind.NextTrack => "Nächster Titel",
        SystemActionKind.PreviousTrack => "Vorheriger Titel",
        SystemActionKind.StopMedia => "Wiedergabe stoppen",
        SystemActionKind.VolumeUp => "Lauter",
        SystemActionKind.VolumeDown => "Leiser",
        SystemActionKind.ToggleSystemMute => "Ton stumm (an/aus)",
        SystemActionKind.ToggleMicrophoneMute => "Mikrofon stumm (an/aus)",
        SystemActionKind.MicrophoneMuteOn => "Mikrofon stummschalten",
        SystemActionKind.MicrophoneMuteOff => "Mikrofon aktivieren",
        _ => "Windows-Aktion"
    };

    public SystemActionKind Kind { get; set; } = SystemActionKind.PlayPause;
}
