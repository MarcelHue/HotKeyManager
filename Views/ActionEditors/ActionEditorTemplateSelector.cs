using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>
/// Picks the editor template for an action-type ViewModel.
/// A new action type needs a template property here and a case in SelectTemplateCore.
/// </summary>
public class ActionEditorTemplateSelector : DataTemplateSelector
{
    public DataTemplate? WebhookTemplate { get; set; }
    public DataTemplate? KeySequenceTemplate { get; set; }
    public DataTemplate? ProcessTemplate { get; set; }
    public DataTemplate? BatchTemplate { get; set; }
    public DataTemplate? SendTextTemplate { get; set; }
    public DataTemplate? DelayTemplate { get; set; }
    public DataTemplate? MacroTemplate { get; set; }
    public DataTemplate? SystemTemplate { get; set; }
    public DataTemplate? MidiSendTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container) => item switch
    {
        WebhookEditorViewModel => this.WebhookTemplate,
        KeySequenceEditorViewModel => this.KeySequenceTemplate,
        ProcessEditorViewModel => this.ProcessTemplate,
        BatchEditorViewModel => this.BatchTemplate,
        SendTextEditorViewModel => this.SendTextTemplate,
        DelayEditorViewModel => this.DelayTemplate,
        MacroEditorViewModel => this.MacroTemplate,
        SystemEditorViewModel => this.SystemTemplate,
        MidiSendEditorViewModel => this.MidiSendTemplate,
        _ => null
    };

    protected override DataTemplate? SelectTemplateCore(object item) => this.SelectTemplateCore(item, null!);
}
