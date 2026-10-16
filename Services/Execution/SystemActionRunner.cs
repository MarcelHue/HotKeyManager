using HotKeyManager.Models;
using static HotKeyManager.Services.Execution.KeyboardNative;

namespace HotKeyManager.Services.Execution;

/// <summary>Media keys via virtual key codes, microphone mute via the Core Audio API.</summary>
internal static class SystemActionRunner
{
    public static void Run(SystemAction action)
    {
        var keys = ForegroundKeySink.Instance;
        switch (action.Kind)
        {
            case SystemActionKind.PlayPause:
                keys.Tap(VK_MEDIA_PLAY_PAUSE);
                break;
            case SystemActionKind.NextTrack:
                keys.Tap(VK_MEDIA_NEXT_TRACK);
                break;
            case SystemActionKind.PreviousTrack:
                keys.Tap(VK_MEDIA_PREV_TRACK);
                break;
            case SystemActionKind.StopMedia:
                keys.Tap(VK_MEDIA_STOP);
                break;
            case SystemActionKind.VolumeUp:
                keys.Tap(VK_VOLUME_UP);
                break;
            case SystemActionKind.VolumeDown:
                keys.Tap(VK_VOLUME_DOWN);
                break;
            case SystemActionKind.ToggleSystemMute:
                keys.Tap(VK_VOLUME_MUTE);
                break;
            case SystemActionKind.ToggleMicrophoneMute:
                AudioService.SetMicrophoneMute(null);
                break;
            case SystemActionKind.MicrophoneMuteOn:
                AudioService.SetMicrophoneMute(true);
                break;
            case SystemActionKind.MicrophoneMuteOff:
                AudioService.SetMicrophoneMute(false);
                break;
        }
    }
}
