namespace HotKeyManager.Models;

/// <summary>Sends an HTTP request.</summary>
public class WebhookAction : ActionBase
{
    public override ActionType Type => ActionType.Webhook;

    public override string DisplayName => "Webhook";

    public override string Description => $"{this.Method} {this.Url}";

    public string Url { get; set; } = string.Empty;

    public HttpMethodType Method { get; set; } = HttpMethodType.GET;

    public string Body { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/json";

    public Dictionary<string, string> Headers { get; set; } = new();
}
