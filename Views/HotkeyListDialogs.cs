using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views;

/// <summary>The confirmation and input dialogs of the hotkey list.</summary>
internal static class HotkeyListDialogs
{
    public static async Task<bool> ConfirmDeleteAsync(XamlRoot root, string hotkeyName)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Hotkey löschen?",
            Content = $"Möchtest du den Hotkey \"{hotkeyName}\" wirklich löschen?",
            PrimaryButtonText = "Löschen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    /// <summary>Asks for a new folder name; null when cancelled or left empty.</summary>
    public static async Task<string?> AskNewFolderNameAsync(XamlRoot root)
    {
        var input = new TextBox { PlaceholderText = "z.B. Rode Bank 1" };
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Neuer Ordner",
            Content = input,
            PrimaryButtonText = "Zuweisen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
        };

        var confirmed = await dialog.ShowAsync() == ContentDialogResult.Primary;
        var name = input.Text?.Trim();
        return confirmed && !string.IsNullOrEmpty(name) ? name : null;
    }

    /// <summary>Asks for the new name of a folder; null when cancelled.</summary>
    public static async Task<string?> AskFolderRenameAsync(XamlRoot root, string currentName)
    {
        var input = new TextBox { Text = currentName, SelectionStart = currentName.Length };
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Ordner umbenennen",
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = "Der neue Name gilt für alle Hotkeys in diesem Ordner. "
                            + "Ein bereits vorhandener Name legt beide Ordner zusammen.",
                        TextWrapping = TextWrapping.Wrap,
                    },
                    input,
                },
            },
            PrimaryButtonText = "Umbenennen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary ? input.Text : null;
    }

    public static async Task<bool> ConfirmDissolveAsync(XamlRoot root, HotkeyGroup group)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = "Ordner auflösen?",
            Content = $"Die {group.CountText} aus \"{group.Name}\" bleiben erhalten "
                + $"und stehen danach unter \"{HotkeyGroup.NoGroupLabel}\".",
            PrimaryButtonText = "Auflösen",
            CloseButtonText = "Abbrechen",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public static async Task ShowMessageAsync(XamlRoot root, string title, string message)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = message,
            CloseButtonText = "OK",
        };
        await dialog.ShowAsync();
    }
}
