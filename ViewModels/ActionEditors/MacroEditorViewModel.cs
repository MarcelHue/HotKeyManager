using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Block editor for macros with two modes: simple (<see cref="SimpleMacroForm"/>: key before,
/// text, key after) and advanced (the full, sortable block list with all action types).
/// </summary>
public partial class MacroEditorViewModel : ActionEditorViewModelBase
{
    /// <summary>Macros with fewer blocks open fully expanded.</summary>
    private const int ExpandAllBelowStepCount = 3;
    private const int MaxStepDelayMs = 60_000;

    private bool isSwitchingMode;

    [ObservableProperty]
    private double stepDelayMs;

    [ObservableProperty]
    private bool isEmptyHintVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdvancedMode))]
    private bool isSimpleMode = true;

    /// <summary>Keeps the empty hint in sync with the block list.</summary>
    public MacroEditorViewModel()
    {
        this.Steps.CollectionChanged += (_, _) => this.IsEmptyHintVisible = this.Steps.Count == 0;
    }

    public override string DisplayName => "Makro (Bausteine)";

    public override ActionType Type => ActionType.Macro;

    /// <summary>The blocks, each one the editor ViewModel of its action type.</summary>
    public ObservableCollection<ActionEditorViewModelBase> Steps { get; } = new();

    public SimpleMacroForm Simple { get; } = new();

    /// <summary>Inverse of IsSimpleMode, settable for the two-way binding of the mode switch.</summary>
    public bool IsAdvancedMode
    {
        get => !this.IsSimpleMode;
        set => this.IsSimpleMode = !value;
    }

    public override void LoadFrom(ActionBase action)
    {
        if (action is not MacroAction macro)
        {
            return;
        }

        this.StepDelayMs = macro.StepDelayMs;
        this.isSwitchingMode = true;
        try
        {
            if (this.Simple.TryLoad(macro))
            {
                this.IsSimpleMode = true;
                this.Steps.Clear();
                return;
            }

            this.IsSimpleMode = false;
            this.Simple.Clear();
            this.LoadSteps(macro.Steps);
        }
        finally
        {
            this.isSwitchingMode = false;
        }
    }

    /// <summary>
    /// Incomplete or empty blocks are kept as they are: a macro may be saved as a draft.
    /// </summary>
    public override ActionBase BuildAction() => new MacroAction
    {
        Steps = this.IsSimpleMode ? this.Simple.BuildSteps() : this.BuildStepActions(),
        StepDelayMs = Math.Clamp((int)this.StepDelayMs, 0, MaxStepDelayMs),
    };

    public override void CancelCapture()
    {
        this.Simple.CancelCapture();
        foreach (var step in this.Steps)
        {
            step.CancelCapture();
        }
    }

    /// <summary>Adds a block; blocks only exist in advanced mode, so this switches to it.</summary>
    public void AddStep(ActionType type)
    {
        this.IsSimpleMode = false;
        if (CreateEditorFor(type) is not { } editor)
        {
            return;
        }

        // A new block is shown open because it needs to be filled in.
        editor.IsExpanded = true;
        this.Steps.Add(editor);
    }

    private static ActionEditorViewModelBase? CreateEditorFor(ActionType type) => type switch
    {
        ActionType.SendText => new SendTextEditorViewModel(),
        ActionType.KeySequence => new KeySequenceEditorViewModel(),
        ActionType.Delay => new DelayEditorViewModel(),
        ActionType.Webhook => new WebhookEditorViewModel(),
        ActionType.StartProcess => new ProcessEditorViewModel(),
        ActionType.BatchCommand => new BatchEditorViewModel(),
        ActionType.System => new SystemEditorViewModel(),
        ActionType.MidiSend => new MidiSendEditorViewModel(),

        // No macro inside a macro: keeps editor and execution easy to follow.
        _ => null,
    };

    [RelayCommand]
    private void RemoveStep(ActionEditorViewModelBase? step)
    {
        if (step is null)
        {
            return;
        }

        step.CancelCapture();
        this.Steps.Remove(step);
    }

    private List<ActionBase> BuildStepActions() => this.Steps.Select(s => s.BuildAction()).ToList();

    private void LoadSteps(IEnumerable<ActionBase> actions)
    {
        this.Steps.Clear();
        foreach (var action in actions)
        {
            if (CreateEditorFor(action.Type) is { } editor)
            {
                editor.LoadFrom(action);
                this.Steps.Add(editor);
            }
        }

        if (this.Steps.Count < ExpandAllBelowStepCount)
        {
            foreach (var step in this.Steps)
            {
                step.IsExpanded = true;
            }
        }
    }

    partial void OnIsSimpleModeChanged(bool value)
    {
        if (this.isSwitchingMode)
        {
            return;
        }

        this.isSwitchingMode = true;
        try
        {
            if (value)
            {
                // Advanced to simple: take over what fits the simple form, otherwise start empty.
                var macro = new MacroAction { Steps = this.BuildStepActions() };
                if (!this.Simple.TryLoad(macro))
                {
                    this.Simple.Clear();
                }
            }
            else
            {
                // Simple to advanced: turn the fields into real blocks so nothing is lost.
                this.LoadSteps(this.Simple.BuildSteps());
            }
        }
        finally
        {
            this.isSwitchingMode = false;
        }
    }
}
