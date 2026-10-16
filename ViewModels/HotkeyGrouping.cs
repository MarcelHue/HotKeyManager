using HotKeyManager.Models;

namespace HotKeyManager.ViewModels;

/// <summary>Search filter and folder grouping of the hotkey list.</summary>
internal static class HotkeyGrouping
{
    /// <summary>Hotkeys whose name, folder, trigger or action matches the search text.</summary>
    public static IEnumerable<HotkeyDefinition> Filter(IEnumerable<HotkeyDefinition> hotkeys, string filter)
    {
        if (filter.Length == 0)
        {
            return hotkeys;
        }

        return hotkeys.Where(h =>
            Contains(h.Name, filter)
            || Contains(h.Group, filter)
            || Contains(h.TriggerDisplayText, filter)
            || Contains(h.Action?.DisplayName, filter)
            || Contains(h.Action?.Description, filter));
    }

    /// <summary>Hotkeys by folder: named folders alphabetically, "Ohne Ordner" last.</summary>
    public static IEnumerable<IGrouping<string, HotkeyDefinition>> ByFolder(IEnumerable<HotkeyDefinition> hotkeys) =>
        hotkeys
            .GroupBy(h => h.Group ?? string.Empty)
            .OrderBy(g => string.IsNullOrEmpty(g.Key) ? 1 : 0)
            .ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>All folder names in use, sorted, without the empty "no folder" entry.</summary>
    public static List<string> FolderNames(IEnumerable<HotkeyDefinition> hotkeys) => hotkeys
        .Select(h => h.Group)
        .Where(g => !string.IsNullOrWhiteSpace(g))
        .Distinct(StringComparer.CurrentCultureIgnoreCase)
        .OrderBy(g => g, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    private static bool Contains(string? text, string filter) =>
        text?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true;
}
