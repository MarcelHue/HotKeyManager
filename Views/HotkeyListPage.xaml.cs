using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using HotKeyManager.Models;
using HotKeyManager.Services;
using System.Collections.ObjectModel;

namespace HotKeyManager.Views;

public sealed partial class HotkeyListPage : Page
{
    public ObservableCollection<HotkeyDefinition> Hotkeys { get; } = new();
    private bool _suppressToggleHandler = false;

    public HotkeyListPage()
    {
        this.InitializeComponent();

        // Subscribe to changes
        App.Current.HotkeyManagerService.HotkeysChanged += OnHotkeysChanged;

        // Load initial data
        RefreshHotkeys();

        // Set ItemsSource
        HotkeyListView.ItemsSource = Hotkeys;
    }

    private void OnHotkeysChanged(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(() => RefreshHotkeys());
    }

    private void RefreshHotkeys()
    {
        _suppressToggleHandler = true;
        try
        {
            Hotkeys.Clear();
            foreach (var hotkey in App.Current.HotkeyManagerService.Hotkeys)
            {
                Hotkeys.Add(hotkey);
            }
        }
        finally
        {
            _suppressToggleHandler = false;
        }

        UpdateUI();
    }

    private void UpdateUI()
    {
        var count = Hotkeys.Count;
        HotkeyCountText.Text = count == 1 ? "1 Hotkey konfiguriert" : $"{count} Hotkeys konfiguriert";
        EmptyState.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HotkeyListView.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void AddHotkey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new HotkeyEditorDialog
        {
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary && dialog.Hotkey != null)
        {
            App.Current.HotkeyManagerService.AddHotkey(dialog.Hotkey);
        }
    }

    private async void ExecuteHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Guid id)
        {
            var hotkey = App.Current.HotkeyManagerService.Hotkeys.FirstOrDefault(h => h.Id == id);
            if (hotkey?.Action != null)
            {
                var executor = new ActionExecutor();
                await executor.ExecuteAsync(hotkey.Action);
            }
        }
    }

    private async void EditHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Guid id)
        {
            var hotkey = App.Current.HotkeyManagerService.Hotkeys.FirstOrDefault(h => h.Id == id);
            if (hotkey != null)
            {
                var dialog = new HotkeyEditorDialog(hotkey)
                {
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary && dialog.Hotkey != null)
                {
                    App.Current.HotkeyManagerService.UpdateHotkey(dialog.Hotkey);
                }
            }
        }
    }

    private async void DeleteHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Guid id)
        {
            var hotkey = App.Current.HotkeyManagerService.Hotkeys.FirstOrDefault(h => h.Id == id);
            if (hotkey != null)
            {
                var confirmDialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "Hotkey löschen?",
                    Content = $"Möchtest du den Hotkey \"{hotkey.Name}\" wirklich löschen?",
                    PrimaryButtonText = "Löschen",
                    CloseButtonText = "Abbrechen",
                    DefaultButton = ContentDialogButton.Close
                };

                var result = await confirmDialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    App.Current.HotkeyManagerService.RemoveHotkey(id);
                }
            }
        }
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleHandler) return;

        if (sender is ToggleSwitch toggle && toggle.Tag is Guid id)
        {
            var hotkey = App.Current.HotkeyManagerService.Hotkeys.FirstOrDefault(h => h.Id == id);
            if (hotkey != null && hotkey.IsEnabled != toggle.IsOn)
            {
                App.Current.HotkeyManagerService.ToggleHotkey(id);
            }
        }
    }
}
