namespace HotKeyManager.Models;

/// <summary>
/// Runs a list of steps from top to bottom. Steps can be any action type,
/// e.g. for a login flow: text, Tab, text, Enter.
/// </summary>
public class MacroAction : ActionBase
{
    public override ActionType Type => ActionType.Macro;

    public override string DisplayName => "Makro";

    // A single step describes itself better in the list, especially for migrated legacy actions.
    public override string Description => this.Steps.Count == 1
        ? this.Steps[0].Description
        : $"{this.Steps.Count} Bausteine";

    public List<ActionBase> Steps { get; set; } = new();

    /// <summary>Pause between the steps in milliseconds.</summary>
    public int StepDelayMs { get; set; }
}
