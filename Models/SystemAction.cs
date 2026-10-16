namespace HotKeyManager.Models;

/// <summary>Windows system action: media keys and microphone mute.</summary>
public class SystemAction : ActionBase
{
    public override ActionType Type => ActionType.System;

    public override string DisplayName => "Windows-Aktion";

    public override string Description => this.Kind switch
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
