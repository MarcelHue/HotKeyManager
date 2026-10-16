using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// The simple macro form: key before, text, key after. Covers the most common macro (e.g. Ctrl+F,
/// then type a search text) without managing blocks.
/// </summary>
public partial class SimpleMacroForm : ObservableObject
{
    private const int MaxSteps = 3;
    private const int DefaultCharDelayMs = 5;
    private const int MaxCharDelayMs = 500;
    private const int MillisecondsPerSecond = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    private string text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    private double charDelayMs = DefaultCharDelayMs;

    public KeyCaptureField PrefixKey { get; } = new();

    public KeyCaptureField SuffixKey { get; } = new();

    /// <summary>Character count and estimated typing time.</summary>
    public string StatsText
    {
        get
        {
            var length = this.Text?.Length ?? 0;
            var totalMs = length * (int)this.CharDelayMs;
            var duration = totalMs < MillisecondsPerSecond
                ? $"{totalMs} ms"
                : $"{totalMs / (double)MillisecondsPerSecond:0.#} s";
            return $"{length} Zeichen · geschätzte Dauer: {duration}";
        }
    }

    /// <summary>
    /// Loads the macro if it fits the simple form: at most one key before and one after a single text
    /// block, each key block holding exactly one plain key press.
    /// </summary>
    public bool TryLoad(MacroAction macro)
    {
        if (macro.Steps.Count is 0 or > MaxSteps || macro.Steps.OfType<SendTextAction>().Count() != 1)
        {
            return false;
        }

        // Two keys on the same side of the text do not fit the form and must not be dropped silently.
        var textIndex = macro.Steps.FindIndex(s => s is SendTextAction);
        var keysAfterText = macro.Steps.Count - 1 - textIndex;
        if (textIndex > 1 || keysAfterText > 1)
        {
            return false;
        }

        var keyBlocks = macro.Steps.Where((_, i) => i != textIndex).ToList();
        if (!keyBlocks.All(IsSingleKeyPress))
        {
            return false;
        }

        var textStep = (SendTextAction)macro.Steps[textIndex];
        this.Text = textStep.Text;
        this.CharDelayMs = textStep.CharDelayMs;
        SetKey(this.PrefixKey, textIndex > 0 ? macro.Steps[0] as KeySequenceAction : null);
        SetKey(this.SuffixKey, textIndex < macro.Steps.Count - 1 ? macro.Steps[^1] as KeySequenceAction : null);
        return true;
    }

    /// <summary>Empties all fields.</summary>
    public void Clear()
    {
        this.Text = string.Empty;
        this.CharDelayMs = DefaultCharDelayMs;
        SetKey(this.PrefixKey, null);
        SetKey(this.SuffixKey, null);
    }

    /// <summary>The form as macro blocks; empty fields produce no block.</summary>
    public List<ActionBase> BuildSteps()
    {
        var steps = new List<ActionBase>();
        if (this.PrefixKey.ToKeyStroke() is { } prefixStroke)
        {
            steps.Add(new KeySequenceAction { Keys = [prefixStroke] });
        }

        if (!string.IsNullOrEmpty(this.Text))
        {
            steps.Add(new SendTextAction
            {
                Text = this.Text,
                CharDelayMs = Math.Clamp((int)this.CharDelayMs, 0, MaxCharDelayMs),
            });
        }

        if (this.SuffixKey.ToKeyStroke() is { } suffixStroke)
        {
            steps.Add(new KeySequenceAction { Keys = [suffixStroke] });
        }

        return steps;
    }

    /// <summary>Stops a running key capture in either field.</summary>
    public void CancelCapture()
    {
        this.PrefixKey.StopCapture();
        this.SuffixKey.StopCapture();
    }

    private static bool IsSingleKeyPress(ActionBase step) =>
        step is KeySequenceAction { UseKernelInjection: false, Keys: [{ EventType: KeyEventType.KeyPress }] };

    private static void SetKey(KeyCaptureField field, KeySequenceAction? block)
    {
        if (block is null)
        {
            field.Set(0, ModifierKeys.None);
            return;
        }

        field.Set(block.Keys[0].VirtualKeyCode, block.Keys[0].Modifiers);
    }
}
