using CommunityToolkit.Mvvm.ComponentModel;
using HotKeyManager.Models;

namespace HotKeyManager.ViewModels.ActionEditors;

/// <summary>Editor for the block that calls a webhook.</summary>
public partial class WebhookEditorViewModel : ActionEditorViewModelBase
{
    /// <summary>Selectable content types; the order must match the ComboBox items in WebhookEditorView.</summary>
    public static readonly IReadOnlyList<string> ContentTypes = new[]
    {
        "application/json",
        "application/x-www-form-urlencoded",
        "text/plain"
    };

    /// <summary>Selectable HTTP methods in the order of <see cref="HttpMethodType"/>.</summary>
    public static readonly IReadOnlyList<string> HttpMethods = new[]
    {
        "GET", "POST", "PUT", "DELETE", "PATCH"
    };

    // The UI cannot edit headers yet; they are passed through unchanged so saving does not drop them.
    private Dictionary<string, string> headers = new();

    [ObservableProperty]
    private string url = string.Empty;

    [ObservableProperty]
    private int methodIndex;

    [ObservableProperty]
    private int contentTypeIndex;

    [ObservableProperty]
    private string body = string.Empty;

    /// <inheritdoc />
    public override string DisplayName => "Webhook aufrufen";

    /// <inheritdoc />
    public override ActionType Type => ActionType.Webhook;

    /// <inheritdoc />
    public override void LoadFrom(ActionBase action)
    {
        if (action is not WebhookAction webhook)
        {
            return;
        }

        this.Url = webhook.Url;
        this.MethodIndex = (int)webhook.Method;
        this.Body = webhook.Body;
        this.headers = webhook.Headers;

        var index = ContentTypes.ToList().IndexOf(webhook.ContentType);
        this.ContentTypeIndex = index >= 0 ? index : 0;
    }

    /// <inheritdoc />
    public override ActionBase BuildAction() => new WebhookAction
    {
        Url = this.Url.Trim(),
        Method = (HttpMethodType)this.MethodIndex,
        ContentType = ContentTypes[Math.Clamp(this.ContentTypeIndex, 0, ContentTypes.Count - 1)],
        Body = this.Body,
        Headers = this.headers
    };
}
