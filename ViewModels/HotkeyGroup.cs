using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels;

/// <summary>A collapsible folder in the hotkey list; folders are not nested.</summary>
public partial class HotkeyGroup : ObservableObject
{
    /// <summary>Header text for hotkeys that are not in a folder.</summary>
    public const string NoGroupLabel = "Ohne Ordner";

    [ObservableProperty]
    private bool isExpanded = true;

    /// <summary>Creates a folder for the given stored folder name.</summary>
    public HotkeyGroup(string key)
    {
        this.Key = key;
    }

    /// <summary>Stored folder name; empty means no folder.</summary>
    public string Key { get; }

    /// <summary>Display name in the header row.</summary>
    public string Name => string.IsNullOrEmpty(this.Key) ? NoGroupLabel : this.Key;

    /// <summary>The hotkeys in this folder.</summary>
    public ObservableCollection<HotkeyDefinition> Items { get; } = new();

    /// <summary>Hotkey count for the header row.</summary>
    public string CountText => this.Items.Count == 1 ? "1 Hotkey" : $"{this.Items.Count} Hotkeys";
}
