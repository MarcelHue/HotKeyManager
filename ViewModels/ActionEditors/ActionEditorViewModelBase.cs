using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Base class for all action type editors.
/// A new action type needs a model class, an editor view model (derived from this class),
/// an editor UserControl, a DataTemplate in the template selector and an entry in
/// HotkeyEditorViewModel.ActionEditors.
/// </summary>
public abstract partial class ActionEditorViewModelBase : ObservableObject
{
    /// <summary>
    /// Expanded state in the macro editor. Short macros load fully expanded
    /// so everything is visible without extra clicks.
    /// </summary>
    [ObservableProperty]
    private bool isExpanded;

    /// <summary>Display name in the action type drop-down.</summary>
    public abstract string DisplayName { get; }

    /// <summary>The action type this editor edits.</summary>
    public abstract ActionType Type { get; }

    /// <summary>Fills the editor fields from an existing action.</summary>
    public abstract void LoadFrom(ActionBase action);

    /// <summary>
    /// Builds the action from the editor fields. Always returns an object, even for empty fields,
    /// so macros can be saved as drafts.
    /// </summary>
    public abstract ActionBase BuildAction();

    /// <summary>Stops running key captures or recordings, e.g. when the user leaves the page.</summary>
    public virtual void CancelCapture()
    {
    }
}
