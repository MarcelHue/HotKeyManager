using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>Editor for the delay block, which is only offered inside macros.</summary>
public partial class DelayEditorViewModel : ActionEditorViewModelBase
{
    private const int DefaultDurationMs = 500;

    // Must match NumberBox.Maximum in DelayEditorView.
    private const int MaxDurationMs = 600_000;

    [ObservableProperty]
    private double durationMs = DefaultDurationMs;

    /// <inheritdoc />
    public override string DisplayName => "Verzögerung";

    /// <inheritdoc />
    public override ActionType Type => ActionType.Delay;

    /// <inheritdoc />
    public override void LoadFrom(ActionBase action)
    {
        if (action is not DelayAction delay)
        {
            return;
        }

        this.DurationMs = delay.DurationMs;
    }

    /// <inheritdoc />
    public override ActionBase BuildAction() => new DelayAction
    {
        DurationMs = Math.Clamp((int)this.DurationMs, 0, MaxDurationMs)
    };
}
