using HotKeyManager.Models;
using HotKeyManager.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HotKeyManager.Views;

public sealed partial class HotkeyListPage : Page
{
    public HotkeyListViewModel ViewModel { get; } = new();

    public HotkeyListPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.Activate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Deactivate();
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        ViewModel.SearchText = sender.Text;
    }

    private void AddHotkey_Click(object sender, object e)
    {
        Frame.Navigate(typeof(HotkeyEditorPage), new EditorNavArgs());
    }

    private void AddTextMacro_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(HotkeyEditorPage), new EditorNavArgs { PreselectType = ActionType.SendText });
    }

    private void AddMacro_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(HotkeyEditorPage), new EditorNavArgs { PreselectType = ActionType.Macro });
    }

    private void EditHotkey_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HotkeyDefinition hotkey)
            Frame.Navigate(typeof(HotkeyEditorPage), new EditorNavArgs { Hotkey = hotkey });
    }

    private async void RunHotkey_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is HotkeyDefinition hotkey)
            await ViewModel.RunCommand.ExecuteAsync(hotkey);
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        // OneWay-Binding: der Handler uebernimmt den Wert ins Model und persistiert.
        // Feuert der Event nur durch Container-Recycling, sind Werte gleich -> kein Save.
        if (sender is ToggleSwitch { DataContext: HotkeyDefinition hotkey } toggle
            && hotkey.IsEnabled != toggle.IsOn)
        {
            hotkey.IsEnabled = toggle.IsOn;
            ViewModel.PersistToggle();
        }
    }

    private async void DeleteHotkey_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HotkeyDefinition hotkey)
            return;

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
            ViewModel.Delete(hotkey);
        }
    }
}
