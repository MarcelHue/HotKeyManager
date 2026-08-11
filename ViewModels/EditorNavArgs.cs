using HotKeyManager.Models;

namespace HotKeyManager.ViewModels;

/// <summary>
/// Navigationsparameter fuer die HotkeyEditorPage.
/// Hotkey == null bedeutet Neuanlage; PreselectType waehlt den Baustein vor (z.B. Text-Makro);
/// IsDuplicate laedt alle Einstellungen als neuen Hotkey, aber ohne Ausloeser.
/// </summary>
public class EditorNavArgs
{
    public HotkeyDefinition? Hotkey { get; init; }
    public ActionType? PreselectType { get; init; }
    public bool IsDuplicate { get; init; }
}
