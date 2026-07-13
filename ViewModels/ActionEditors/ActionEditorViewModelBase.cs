using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>
/// Basisklasse fuer alle Aktionstyp-Editoren.
/// Ein neuer Aktionstyp braucht: Model-Klasse, ein Editor-ViewModel (diese Basis),
/// ein Editor-UserControl, ein DataTemplate im TemplateSelector und einen Eintrag
/// in HotkeyEditorViewModel.ActionEditors.
/// </summary>
public abstract partial class ActionEditorViewModelBase : ObservableObject
{
    /// <summary>Anzeigename im Aktionstyp-Dropdown.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Der Aktionstyp, den dieser Editor bearbeitet.</summary>
    public abstract ActionType Type { get; }

    /// <summary>Fehlermeldung, wenn BuildAction() null liefert.</summary>
    public abstract string ValidationMessage { get; }

    /// <summary>Befuellt die Editor-Felder aus einer bestehenden Aktion.</summary>
    public abstract void LoadFrom(ActionBase action);

    /// <summary>Baut die Aktion aus den Editor-Feldern. Null = Eingaben unvollstaendig/ungueltig.</summary>
    public abstract ActionBase? BuildAction();

    /// <summary>Stoppt laufende Capture-/Aufnahme-Vorgaenge (z.B. beim Verlassen der Seite).</summary>
    public virtual void CancelCapture() { }
}
