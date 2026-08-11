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
    /// <summary>
    /// Aufgeklappt-Zustand im Makro-Editor. Kurze Makros werden komplett
    /// aufgeklappt geladen, damit man alles ohne Klicks sieht.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>Anzeigename im Aktionstyp-Dropdown.</summary>
    public abstract string DisplayName { get; }

    /// <summary>Der Aktionstyp, den dieser Editor bearbeitet.</summary>
    public abstract ActionType Type { get; }

    /// <summary>Befuellt die Editor-Felder aus einer bestehenden Aktion.</summary>
    public abstract void LoadFrom(ActionBase action);

    /// <summary>
    /// Baut die Aktion aus den Editor-Feldern. Liefert immer ein Objekt — auch bei
    /// leeren Feldern, damit Makros als Entwurf gespeichert werden koennen.
    /// </summary>
    public abstract ActionBase BuildAction();

    /// <summary>Stoppt laufende Capture-/Aufnahme-Vorgaenge (z.B. beim Verlassen der Seite).</summary>
    public virtual void CancelCapture() { }
}
