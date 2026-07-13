using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

public partial class WebhookEditorViewModel : ActionEditorViewModelBase
{
    public static readonly IReadOnlyList<string> ContentTypes = new[]
    {
        "application/json",
        "application/x-www-form-urlencoded",
        "text/plain"
    };

    public static readonly IReadOnlyList<string> HttpMethods = new[]
    {
        "GET", "POST", "PUT", "DELETE", "PATCH"
    };

    // Headers werden im UI (noch) nicht editiert, aber beim Bearbeiten unveraendert durchgereicht,
    // damit sie beim Speichern nicht verloren gehen.
    private Dictionary<string, string> _headers = new();

    public override string DisplayName => "Webhook aufrufen";
    public override ActionType Type => ActionType.Webhook;
    public override string ValidationMessage => "Bitte gib eine Webhook-URL ein.";

    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private int _methodIndex;

    [ObservableProperty]
    private int _contentTypeIndex;

    [ObservableProperty]
    private string _body = string.Empty;

    public override void LoadFrom(ActionBase action)
    {
        if (action is not WebhookAction webhook) return;

        Url = webhook.Url;
        MethodIndex = (int)webhook.Method;
        Body = webhook.Body;
        _headers = webhook.Headers;

        var index = ContentTypes.ToList().IndexOf(webhook.ContentType);
        ContentTypeIndex = index >= 0 ? index : 0;
    }

    public override ActionBase? BuildAction()
    {
        if (string.IsNullOrWhiteSpace(Url))
            return null;

        return new WebhookAction
        {
            Url = Url.Trim(),
            Method = (HttpMethodType)MethodIndex,
            ContentType = ContentTypes[Math.Clamp(ContentTypeIndex, 0, ContentTypes.Count - 1)],
            Body = Body,
            Headers = _headers
        };
    }
}
