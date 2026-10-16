using HotKeyManager.Models;

namespace HotKeyManager.ViewModels;

/// <summary>Navigation parameter for the HotkeyEditorPage.</summary>
public class EditorNavArgs
{
    /// <summary>The hotkey to edit, or null to create a new one.</summary>
    public HotkeyDefinition? Hotkey { get; init; }

    /// <summary>The block to preselect for a new hotkey, e.g. a text macro.</summary>
    public ActionType? PreselectType { get; init; }

    /// <summary>Loads all settings of <see cref="Hotkey"/> as a new hotkey, but without its trigger.</summary>
    public bool IsDuplicate { get; init; }
}
