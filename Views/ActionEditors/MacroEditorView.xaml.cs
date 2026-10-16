using HotKeyManager.Models;
using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>Editor UI of a macro: simple form or block list.</summary>
public sealed partial class MacroEditorView : UserControl
{
    /// <summary>Refreshes the x:Bind bindings whenever the macro behind the view changes.</summary>
    public MacroEditorView()
    {
        this.InitializeComponent();
        this.DataContextChanged += (_, _) => this.Bindings.Update();
    }

    public MacroEditorViewModel? ViewModel => this.DataContext as MacroEditorViewModel;

    /// <summary>Explains the current mode below the mode switch.</summary>
    public string ModeHint(bool isSimpleMode) => isSimpleMode
        ? "Einfach: Taste davor, Text, Taste danach — für die häufigsten Makros."
        : "Erweitert: beliebige Bausteine frei kombinierbar.";

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (this.ViewModel is null || sender is not MenuFlyoutItem { Tag: string tag })
        {
            return;
        }

        ActionType? type = tag switch
        {
            "sendtext" => ActionType.SendText,
            "keysequence" => ActionType.KeySequence,
            "delay" => ActionType.Delay,
            "system" => ActionType.System,
            "midisend" => ActionType.MidiSend,
            "webhook" => ActionType.Webhook,
            "process" => ActionType.StartProcess,
            "batch" => ActionType.BatchCommand,
            _ => null,
        };

        if (type is { } actionType)
        {
            this.ViewModel.AddStep(actionType);
        }
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ActionEditorViewModelBase step })
        {
            this.ViewModel?.RemoveStepCommand.Execute(step);
        }
    }
}
