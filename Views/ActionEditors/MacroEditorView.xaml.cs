using HotKeyManager.Models;
using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

public sealed partial class MacroEditorView : UserControl
{
    public MacroEditorViewModel? ViewModel => DataContext as MacroEditorViewModel;

    public MacroEditorView()
    {
        this.InitializeComponent();
        DataContextChanged += (s, e) => Bindings.Update();
    }

    public string ModeHint(bool isSimpleMode) => isSimpleMode
        ? "Einfach: Taste davor, Text, Taste danach — für die häufigsten Makros."
        : "Erweitert: beliebige Bausteine frei kombinierbar.";

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null || sender is not MenuFlyoutItem { Tag: string tag }) return;

        var type = tag switch
        {
            "sendtext" => ActionType.SendText,
            "keysequence" => ActionType.KeySequence,
            "delay" => ActionType.Delay,
            "system" => ActionType.System,
            "midisend" => ActionType.MidiSend,
            "webhook" => ActionType.Webhook,
            "process" => ActionType.StartProcess,
            "batch" => ActionType.BatchCommand,
            _ => (ActionType?)null
        };

        if (type is { } actionType)
            ViewModel.AddStep(actionType);
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ActionEditorViewModelBase step)
            ViewModel?.RemoveStepCommand.Execute(step);
    }
}
