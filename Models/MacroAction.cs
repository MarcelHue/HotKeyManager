namespace HotKeyManager.Models;

/// <summary>
/// Baustein-Makro: fuehrt eine Liste von Aktionen von oben nach unten aus.
/// Bausteine koennen alle Aktionstypen sein (Text, Tastensequenz, Verzoegerung,
/// Webhook, Prozess, Batch) — z.B. fuer Login-Ablaeufe: Text → Tab → Text → Enter.
/// </summary>
public class MacroAction : ActionBase
{
    public override ActionType Type => ActionType.Macro;
    public override string DisplayName => "Makro";
    // Bei genau einem Baustein die Beschreibung des Bausteins zeigen (aussagekraeftiger
    // in der Liste, v.a. fuer migrierte Alt-Aktionen)
    public override string Description => Steps.Count == 1
        ? Steps[0].Description
        : $"{Steps.Count} Bausteine";

    public List<ActionBase> Steps { get; set; } = new();

    /// <summary>Globale Verzoegerung zwischen den Bausteinen in Millisekunden.</summary>
    public int StepDelayMs { get; set; } = 0;
}
