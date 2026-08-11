using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels;

/// <summary>
/// Ein Ordner in der Hotkey-Liste (eine Ebene, einklappbar).
/// </summary>
public partial class HotkeyGroup : ObservableObject
{
    public const string NoGroupLabel = "Ohne Ordner";

    /// <summary>Gespeicherter Ordnername; leer = kein Ordner.</summary>
    public string Key { get; }

    /// <summary>Anzeigename in der Kopfzeile.</summary>
    public string Name => string.IsNullOrEmpty(Key) ? NoGroupLabel : Key;

    public ObservableCollection<HotkeyDefinition> Items { get; } = new();

    public string CountText => Items.Count == 1 ? "1 Hotkey" : $"{Items.Count} Hotkeys";

    [ObservableProperty]
    private bool _isExpanded = true;

    public HotkeyGroup(string key)
    {
        Key = key;
    }
}
