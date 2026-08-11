using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Baustein-Editor fuer Makros mit zwei Modi:
///
/// Einfach (Standard): feste Felder "Taste davor / Text / Taste danach" — deckt den
/// haeufigsten Fall ohne Baustein-Verwaltung ab (z.B. Ctrl+F, dann Suchtext tippen).
///
/// Erweitert: die volle, sortierbare Baustein-Liste mit allen Aktionstypen.
/// </summary>
public partial class MacroEditorViewModel : ActionEditorViewModelBase
{
    private string _validationMessage = "Bitte füge mindestens einen Baustein hinzu.";
    private bool _isSwitchingMode;

    public override string DisplayName => "Makro (Bausteine)";
    public override ActionType Type => ActionType.Macro;
    public override string ValidationMessage => _validationMessage;

    /// <summary>Die Bausteine — jeweils das Editor-ViewModel des zugehoerigen Aktionstyps.</summary>
    public ObservableCollection<ActionEditorViewModelBase> Steps { get; } = new();

    [ObservableProperty]
    private double _stepDelayMs;

    [ObservableProperty]
    private bool _isEmptyHintVisible = true;

    #region Einfach-Modus

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAdvancedMode))]
    private bool _isSimpleMode = true;

    /// <summary>Invers zu IsSimpleMode — setzbar fuer die TwoWay-Bindung des Umschalters.</summary>
    public bool IsAdvancedMode
    {
        get => !IsSimpleMode;
        set => IsSimpleMode = !value;
    }

    public KeyCaptureField PrefixKey { get; } = new();
    public KeyCaptureField SuffixKey { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimpleStatsText))]
    private string _simpleText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimpleStatsText))]
    private double _simpleCharDelayMs = 5;

    public string SimpleStatsText
    {
        get
        {
            var length = SimpleText?.Length ?? 0;
            var totalMs = length * (int)SimpleCharDelayMs;
            var duration = totalMs < 1000 ? $"{totalMs} ms" : $"{totalMs / 1000.0:0.#} s";
            return $"{length} Zeichen · geschätzte Dauer: {duration}";
        }
    }

    #endregion

    public MacroEditorViewModel()
    {
        Steps.CollectionChanged += (s, e) => IsEmptyHintVisible = Steps.Count == 0;
    }

    partial void OnIsSimpleModeChanged(bool value)
    {
        if (_isSwitchingMode) return;

        _isSwitchingMode = true;
        try
        {
            if (!value)
            {
                // Einfach -> Erweitert: Felder in echte Bausteine ueberfuehren,
                // damit nichts verloren geht
                Steps.Clear();
                foreach (var step in BuildSimpleSteps())
                {
                    var editor = CreateEditorFor(step.Type);
                    if (editor == null) continue;
                    editor.LoadFrom(step);
                    Steps.Add(editor);
                }

                ExpandIfShort();
            }
            else
            {
                // Erweitert -> Einfach: uebernehmen, was ins einfache Schema passt
                var action = BuildFromSteps();
                if (action is MacroAction macro && TryLoadSimple(macro))
                {
                    // uebernommen
                }
                else
                {
                    ClearSimpleFields();
                }
            }
        }
        finally
        {
            _isSwitchingMode = false;
        }
    }

    public override void LoadFrom(ActionBase action)
    {
        if (action is not MacroAction macro) return;

        StepDelayMs = macro.StepDelayMs;

        _isSwitchingMode = true;
        try
        {
            // Passt das Makro ins einfache Schema? Dann im Einfach-Modus oeffnen.
            if (TryLoadSimple(macro))
            {
                IsSimpleMode = true;
                Steps.Clear();
                return;
            }

            IsSimpleMode = false;
            ClearSimpleFields();

            Steps.Clear();
            foreach (var step in macro.Steps)
            {
                var editor = CreateEditorFor(step.Type);
                if (editor == null) continue;
                editor.LoadFrom(step);
                Steps.Add(editor);
            }

            ExpandIfShort();
        }
        finally
        {
            _isSwitchingMode = false;
        }
    }

    /// <summary>Kurze Makros (unter 3 Bausteinen) komplett aufgeklappt anzeigen.</summary>
    private void ExpandIfShort()
    {
        if (Steps.Count >= 3) return;

        foreach (var step in Steps)
            step.IsExpanded = true;
    }

    /// <summary>
    /// Erkennt das einfache Schema: [Taste] → Text → [Taste], mit jeweils genau
    /// einer Taste pro Tastensequenz-Baustein.
    /// </summary>
    private bool TryLoadSimple(MacroAction macro)
    {
        if (macro.Steps.Count is 0 or > 3) return false;

        var textSteps = macro.Steps.OfType<SendTextAction>().ToList();
        if (textSteps.Count != 1) return false;

        var textIndex = macro.Steps.FindIndex(s => s is SendTextAction);
        KeySequenceAction? prefix = null;
        KeySequenceAction? suffix = null;

        for (int i = 0; i < macro.Steps.Count; i++)
        {
            if (i == textIndex) continue;

            if (macro.Steps[i] is not KeySequenceAction keySeq
                || keySeq.Keys.Count != 1
                || keySeq.Keys[0].EventType != KeyEventType.KeyPress
                || keySeq.UseKernelInjection)
            {
                return false;
            }

            if (i < textIndex) prefix = keySeq;
            else suffix = keySeq;
        }

        var text = textSteps[0];
        SimpleText = text.Text;
        SimpleCharDelayMs = text.CharDelayMs;

        if (prefix != null) PrefixKey.Set(prefix.Keys[0].VirtualKeyCode, prefix.Keys[0].Modifiers);
        else PrefixKey.Set(0, ModifierKeys.None);

        if (suffix != null) SuffixKey.Set(suffix.Keys[0].VirtualKeyCode, suffix.Keys[0].Modifiers);
        else SuffixKey.Set(0, ModifierKeys.None);

        return true;
    }

    private void ClearSimpleFields()
    {
        SimpleText = string.Empty;
        SimpleCharDelayMs = 5;
        PrefixKey.Set(0, ModifierKeys.None);
        SuffixKey.Set(0, ModifierKeys.None);
    }

    private List<ActionBase> BuildSimpleSteps()
    {
        var steps = new List<ActionBase>();

        if (PrefixKey.ToKeyStroke() is { } prefixStroke)
            steps.Add(new KeySequenceAction { Keys = new List<KeyStroke> { prefixStroke } });

        if (!string.IsNullOrEmpty(SimpleText))
            steps.Add(new SendTextAction
            {
                Text = SimpleText,
                CharDelayMs = Math.Clamp((int)SimpleCharDelayMs, 0, 500)
            });

        if (SuffixKey.ToKeyStroke() is { } suffixStroke)
            steps.Add(new KeySequenceAction { Keys = new List<KeyStroke> { suffixStroke } });

        return steps;
    }

    public override ActionBase? BuildAction()
    {
        if (IsSimpleMode)
        {
            var steps = BuildSimpleSteps();
            if (steps.Count == 0)
            {
                _validationMessage = "Bitte gib einen Text ein oder erfasse eine Taste.";
                return null;
            }

            return new MacroAction
            {
                Steps = steps,
                StepDelayMs = Math.Clamp((int)StepDelayMs, 0, 60_000)
            };
        }

        return BuildFromSteps();
    }

    private ActionBase? BuildFromSteps()
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
        PrefixKey.StopCapture();
        SuffixKey.StopCapture();

        foreach (var step in Steps)
            step.CancelCapture();
    }

    public void AddStep(ActionType type)
    {
        // Bausteine gibt es nur im erweiterten Modus
        IsSimpleMode = false;

        var editor = CreateEditorFor(type);
        if (editor == null) return;

        // Neu hinzugefuegte Bausteine gleich offen zeigen — man will sie ausfuellen
        editor.IsExpanded = true;
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
        ActionType.System => new SystemEditorViewModel(),
        ActionType.MidiSend => new MidiSendEditorViewModel(),
        // Kein Makro-in-Makro — haelt Editor und Ausfuehrung ueberschaubar
        _ => null
    };
}
