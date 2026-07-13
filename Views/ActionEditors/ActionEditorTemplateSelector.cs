using HotKeyManager.ViewModels.ActionEditors;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HotKeyManager.Views.ActionEditors;

/// <summary>
/// Waehlt das passende Editor-Template fuer das jeweilige Aktionstyp-ViewModel.
/// Neuer Aktionstyp: Template-Property ergaenzen + Zuordnung in SelectTemplateCore.
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

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return item switch
        {
            WebhookEditorViewModel => WebhookTemplate,
            KeySequenceEditorViewModel => KeySequenceTemplate,
            ProcessEditorViewModel => ProcessTemplate,
            BatchEditorViewModel => BatchTemplate,
            SendTextEditorViewModel => SendTextTemplate,
            DelayEditorViewModel => DelayTemplate,
            MacroEditorViewModel => MacroTemplate,
            _ => null
        };
    }

    protected override DataTemplate? SelectTemplateCore(object item) => SelectTemplateCore(item, null!);
}
