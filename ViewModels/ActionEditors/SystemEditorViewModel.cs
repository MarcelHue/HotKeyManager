using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

public partial class SystemEditorViewModel : ActionEditorViewModelBase
{
    /// <summary>Reihenfolge muss zu den ComboBox-Eintraegen in SystemEditorView passen.</summary>
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

    public override string DisplayName => "Windows-Aktion";
    public override ActionType Type => ActionType.System;

    [ObservableProperty]
    private int _kindIndex;

    public override void LoadFrom(ActionBase action)
    {
        if (action is not SystemAction system) return;

        var index = Array.IndexOf(Kinds, system.Kind);
        KindIndex = index >= 0 ? index : 0;
    }

    public override ActionBase BuildAction()
    {
        return new SystemAction
        {
            Kind = Kinds[Math.Clamp(KindIndex, 0, Kinds.Length - 1)]
        };
    }
}
