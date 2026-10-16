using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace HotKeyManager.Views;

/// <summary>Builds the flyout menus of the hotkey list rows and folder headers.</summary>
internal static class HotkeyListMenus
{
    private const string NewFolderGlyph = "\uE710";
    private const string RenameGlyph = "\uE8AC";
    private const string DissolveGlyph = "\uE711";
    private const string MoveUpGlyph = "\uE74A";
    private const string MoveDownGlyph = "\uE74B";
    private const string FolderGlyph = "\uE8B7";
    private const string NewFolderText = "Neuer Ordner…";

    /// <summary>"Ohne Ordner", the existing folders and "new folder".</summary>
    public static MenuFlyout AssignFolder(IEnumerable<string> folders, Action<string> assign, Action createFolder)
    {
        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };
        var folderItems = FolderItems(folders, assign).ToList();

        flyout.Items.Add(folderItems[0]);
        if (folderItems.Count > 1)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
        }

        foreach (var item in folderItems.Skip(1))
        {
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(Item(NewFolderText, NewFolderGlyph, createFolder));
        return flyout;
    }

    /// <summary>Up/down plus "move to folder"; ordering is disabled while a search is active.</summary>
    public static MenuFlyout MoveRow(
        bool canReorder,
        Action<int> moveBy,
        IEnumerable<string> folders,
        Action<string> assign,
        Action createFolder)
    {
        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };
        flyout.Items.Add(Item("Nach oben", MoveUpGlyph, () => moveBy(-1), canReorder));
        flyout.Items.Add(Item("Nach unten", MoveDownGlyph, () => moveBy(1), canReorder));
        flyout.Items.Add(new MenuFlyoutSeparator());

        var toFolder = new MenuFlyoutSubItem
        {
            Text = "In Ordner verschieben",
            Icon = new FontIcon { Glyph = FolderGlyph },
        };
        foreach (var item in FolderItems(folders, assign))
        {
            toFolder.Items.Add(item);
        }

        toFolder.Items.Add(new MenuFlyoutSeparator());
        toFolder.Items.Add(Item(NewFolderText, null, createFolder));
        flyout.Items.Add(toFolder);

        if (!canReorder)
        {
            flyout.Items.Add(new MenuFlyoutSeparator());
            const string searchHint = "Sortieren erst ohne aktive Suche möglich";
            flyout.Items.Add(new MenuFlyoutItem { Text = searchHint, IsEnabled = false });
        }

        return flyout;
    }

    /// <summary>Rename and dissolve; "Ohne Ordner" is no real folder, so both are disabled there.</summary>
    public static MenuFlyout Folder(HotkeyGroup group, Action rename, Action dissolve)
    {
        var isRealFolder = !string.IsNullOrEmpty(group.Key);
        var flyout = new MenuFlyout { Placement = FlyoutPlacementMode.Bottom };
        flyout.Items.Add(Item("Ordner umbenennen…", RenameGlyph, rename, isRealFolder));
        flyout.Items.Add(Item("Ordner auflösen", DissolveGlyph, dissolve, isRealFolder));
        return flyout;
    }

    private static IEnumerable<MenuFlyoutItem> FolderItems(IEnumerable<string> folders, Action<string> assign)
    {
        yield return Item(HotkeyGroup.NoGroupLabel, null, () => assign(string.Empty));
        foreach (var folder in folders)
        {
            yield return Item(folder, null, () => assign(folder));
        }
    }

    private static MenuFlyoutItem Item(string text, string? glyph, Action click, bool isEnabled = true)
    {
        var item = new MenuFlyoutItem { Text = text, IsEnabled = isEnabled };
        if (glyph is not null)
        {
            item.Icon = new FontIcon { Glyph = glyph };
        }

        item.Click += (_, _) => click();
        return item;
    }
}
