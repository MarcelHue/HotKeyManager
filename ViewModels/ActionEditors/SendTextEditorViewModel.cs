using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Editor for text macros: multi-line text that is typed key by key
/// with a configurable typing speed.
/// </summary>
public partial class SendTextEditorViewModel : ActionEditorViewModelBase
{
    private const int DefaultCharDelayMs = 5;

    // Must match NumberBox.Maximum in SendTextEditorView.
    private const int MaxCharDelayMs = 500;

    private const int MillisecondsPerSecond = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    private string text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    private double charDelayMs = DefaultCharDelayMs;

    /// <inheritdoc />
    public override string DisplayName => "Text-Makro (Text senden)";

    /// <inheritdoc />
    public override ActionType Type => ActionType.SendText;

    /// <summary>Character count and estimated typing time, shown below the text box.</summary>
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

    /// <inheritdoc />
    public override void LoadFrom(ActionBase action)
    {
        if (action is not SendTextAction sendText)
        {
            return;
        }

        this.Text = sendText.Text;
        this.CharDelayMs = sendText.CharDelayMs;
    }

    /// <inheritdoc />
    public override ActionBase BuildAction() => new SendTextAction
    {
        Text = this.Text ?? string.Empty,
        CharDelayMs = Math.Clamp((int)this.CharDelayMs, 0, MaxCharDelayMs)
    };
}
