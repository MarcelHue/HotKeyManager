using HotKeyManager.Models;

namespace HotKeyManager.ViewModels;

/// <summary>Renames a hotkey directly in its list row; the edit is buffered until Enter or focus loss.</summary>
public sealed class InlineRename(Action saveChanges)
{
    /// <summary>Switches the row into rename mode.</summary>
    public void Begin(HotkeyDefinition hotkey)
    {
        hotkey.EditingName = hotkey.Name;
        hotkey.IsEditingName = true;
    }

    /// <summary>Takes over the edited name; an empty or unchanged name is ignored.</summary>
    public void Commit(HotkeyDefinition hotkey)
    {
        if (!hotkey.IsEditingName)
        {
            return;
        }

        var newName = hotkey.EditingName?.Trim() ?? string.Empty;
        hotkey.IsEditingName = false;
        if (newName.Length == 0 || newName == hotkey.Name)
        {
            return;
        }

        hotkey.Name = newName;
        saveChanges();
    }

    /// <summary>Leaves rename mode without changes.</summary>
    public void Cancel(HotkeyDefinition hotkey) => hotkey.IsEditingName = false;
}
