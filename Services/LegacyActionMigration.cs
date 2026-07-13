using HotKeyManager.Models;

namespace HotKeyManager.Services;

/// <summary>
/// Ueberfuehrt Alt-Aktionen (direkt zugewiesene Einzelaktionen) in Ein-Baustein-Makros.
/// Seit v1.3.0 ist das Baustein-Makro der einzige Top-Level-Aktionstyp.
/// </summary>
public static class LegacyActionMigration
{
    /// <summary>
    /// Wrappt alle Nicht-Makro-Aktionen in ein MacroAction mit einem Baustein.
    /// Liefert true, wenn etwas geaendert wurde (dann sollte gespeichert werden).
    /// </summary>
    public static bool WrapLegacyActions(IEnumerable<HotkeyDefinition> hotkeys)
    {
        var changed = false;

        foreach (var hotkey in hotkeys)
        {
            if (hotkey.Action != null && hotkey.Action is not MacroAction)
            {
                hotkey.Action = new MacroAction
                {
                    Steps = new List<ActionBase> { hotkey.Action }
                };
                changed = true;
            }
        }

        return changed;
    }
}
