using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>Editor for the Windows action block (media keys, volume, microphone mute).</summary>
public partial class SystemEditorViewModel : ActionEditorViewModelBase
{
    // The order must match the ComboBox items in SystemEditorView.
    private static readonly SystemActionKind[] Kinds =
    {
        SystemActionKind.PlayPause,
        SystemActionKind.NextTrack,
        SystemActionKind.PreviousTrack,
        SystemActionKind.StopMedia,
        SystemActionKind.VolumeUp,
        SystemActionKind.VolumeDown,
        SystemActionKind.ToggleSystemMute,
        SystemActionKind.ToggleMicrophoneMute,
        SystemActionKind.MicrophoneMuteOn,
        SystemActionKind.MicrophoneMuteOff
    };

    [ObservableProperty]
    private int kindIndex;

    /// <inheritdoc />
    public override string DisplayName => "Windows-Aktion";

    /// <inheritdoc />
    public override ActionType Type => ActionType.System;

    /// <inheritdoc />
    public override void LoadFrom(ActionBase action)
    {
        if (action is not SystemAction system)
        {
            return;
        }

        var index = Array.IndexOf(Kinds, system.Kind);
        this.KindIndex = index >= 0 ? index : 0;
    }

    /// <inheritdoc />
    public override ActionBase BuildAction() => new SystemAction
    {
        Kind = Kinds[Math.Clamp(this.KindIndex, 0, Kinds.Length - 1)]
    };
}
