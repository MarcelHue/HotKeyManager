using HotKeyManager.Models;

namespace HotKeyManager.Services;

/// <summary>
/// Converts legacy actions (a single action assigned directly) into one-step macros.
/// Since v1.3.0 the macro is the only top-level action type.
/// </summary>
public static class LegacyActionMigration
{
    /// <summary>Wraps every non-macro action in a macro with a single step.</summary>
    /// <returns>True when something changed, so the configuration should be saved.</returns>
    public static bool WrapLegacyActions(IEnumerable<HotkeyDefinition> hotkeys)
    {
        var changed = false;

        foreach (var hotkey in hotkeys)
        {
            if (hotkey.Action is not null and not MacroAction)
            {
                hotkey.Action = new MacroAction { Steps = new List<ActionBase> { hotkey.Action } };
                changed = true;
            }
        }

        return changed;
    }
}
