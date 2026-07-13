using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>Editor fuer den Verzoegerungs-Baustein (nur innerhalb von Makros angeboten).</summary>
public partial class DelayEditorViewModel : ActionEditorViewModelBase
{
    public override string DisplayName => "Verzögerung";
    public override ActionType Type => ActionType.Delay;
    public override string ValidationMessage => "Bitte gib eine gültige Dauer an.";

    [ObservableProperty]
    private double _durationMs = 500;

    public override void LoadFrom(ActionBase action)
    {
        if (action is not DelayAction delay) return;
        DurationMs = delay.DurationMs;
    }

    public override ActionBase? BuildAction()
    {
        return new DelayAction
        {
            DurationMs = Math.Clamp((int)DurationMs, 0, 600_000)
        };
    }
}
