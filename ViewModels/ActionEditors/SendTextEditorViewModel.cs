using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Editor fuer Text-Makros: mehrzeiliger Text, der per Tastendruck getippt wird,
/// mit konfigurierbarer Tippgeschwindigkeit.
/// </summary>
public partial class SendTextEditorViewModel : ActionEditorViewModelBase
{
    public override string DisplayName => "Text-Makro (Text senden)";
    public override ActionType Type => ActionType.SendText;
    public override string ValidationMessage => "Bitte gib den Text ein, der gesendet werden soll.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    private string _text = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    private double _charDelayMs = 5;

    /// <summary>Zeichenanzahl und geschaetzte Tipp-Dauer fuer die Anzeige unter dem Textfeld.</summary>
    public string StatsText
    {
        get
        {
            var length = Text?.Length ?? 0;
            var totalMs = length * (int)CharDelayMs;
            var duration = totalMs < 1000
                ? $"{totalMs} ms"
                : $"{totalMs / 1000.0:0.#} s";
            return $"{length} Zeichen · geschätzte Dauer: {duration}";
        }
    }

    public override void LoadFrom(ActionBase action)
    {
        if (action is not SendTextAction sendText) return;

        Text = sendText.Text;
        CharDelayMs = sendText.CharDelayMs;
    }

    public override ActionBase? BuildAction()
    {
        if (string.IsNullOrWhiteSpace(Text))
            return null;

        return new SendTextAction
        {
            Text = Text,
            CharDelayMs = Math.Clamp((int)CharDelayMs, 0, 500)
        };
    }
}
