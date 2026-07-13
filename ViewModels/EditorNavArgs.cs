using HotKeyManager.Models;

namespace HotKeyManager.ViewModels;

/// <summary>
/// Navigationsparameter fuer die HotkeyEditorPage.
/// Hotkey == null bedeutet Neuanlage; PreselectType waehlt den Aktionstyp vor (z.B. Text-Makro).
/// </summary>
public class EditorNavArgs
{
    public HotkeyDefinition? Hotkey { get; init; }
    public ActionType? PreselectType { get; init; }
}
