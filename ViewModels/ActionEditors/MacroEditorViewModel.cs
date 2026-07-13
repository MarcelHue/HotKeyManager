using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Baustein-Editor fuer Makros: eine sortierbare Liste von Aktions-Bausteinen,
/// die von oben nach unten abgearbeitet werden. Jeder Baustein nutzt den
/// vorhandenen Editor seines Aktionstyps.
/// </summary>
public partial class MacroEditorViewModel : ActionEditorViewModelBase
{
    private string _validationMessage = "Bitte füge mindestens einen Baustein hinzu.";

    public override string DisplayName => "Makro (Bausteine)";
    public override ActionType Type => ActionType.Macro;
    public override string ValidationMessage => _validationMessage;

    /// <summary>Die Bausteine — jeweils das Editor-ViewModel des zugehoerigen Aktionstyps.</summary>
    public ObservableCollection<ActionEditorViewModelBase> Steps { get; } = new();

    [ObservableProperty]
    private double _stepDelayMs;

    [ObservableProperty]
    private bool _isEmptyHintVisible = true;

    public MacroEditorViewModel()
    {
        Steps.CollectionChanged += (s, e) => IsEmptyHintVisible = Steps.Count == 0;
    }

    public override void LoadFrom(ActionBase action)
    {
        if (action is not MacroAction macro) return;

        StepDelayMs = macro.StepDelayMs;

        Steps.Clear();
        foreach (var step in macro.Steps)
        {
            var editor = CreateEditorFor(step.Type);
            if (editor == null) continue;
            editor.LoadFrom(step);
            Steps.Add(editor);
        }
    }

    public override ActionBase? BuildAction()
    {
        if (Steps.Count == 0)
        {
            _validationMessage = "Bitte füge mindestens einen Baustein hinzu.";
            return null;
        }

        var steps = new List<ActionBase>();
        for (int i = 0; i < Steps.Count; i++)
        {
            var stepAction = Steps[i].BuildAction();
            if (stepAction == null)
            {
                _validationMessage = $"Baustein {i + 1} ({Steps[i].DisplayName}): {Steps[i].ValidationMessage}";
                return null;
            }
            steps.Add(stepAction);
        }

        return new MacroAction
        {
            Steps = steps,
            StepDelayMs = Math.Clamp((int)StepDelayMs, 0, 60_000)
        };
    }

    public override void CancelCapture()
    {
        foreach (var step in Steps)
            step.CancelCapture();
    }

    public void AddStep(ActionType type)
    {
        var editor = CreateEditorFor(type);
        if (editor != null)
            Steps.Add(editor);
    }

    [RelayCommand]
    private void RemoveStep(ActionEditorViewModelBase? step)
    {
        if (step == null) return;
        step.CancelCapture();
        Steps.Remove(step);
    }

    private static ActionEditorViewModelBase? CreateEditorFor(ActionType type) => type switch
    {
        ActionType.SendText => new SendTextEditorViewModel(),
        ActionType.KeySequence => new KeySequenceEditorViewModel(),
        ActionType.Delay => new DelayEditorViewModel(),
        ActionType.Webhook => new WebhookEditorViewModel(),
        ActionType.StartProcess => new ProcessEditorViewModel(),
        ActionType.BatchCommand => new BatchEditorViewModel(),
        // Kein Makro-in-Makro — haelt Editor und Ausfuehrung ueberschaubar
        _ => null
    };
}
