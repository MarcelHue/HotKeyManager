namespace HotKeyManager.Models;

public class WebhookAction : ActionBase
{
    public override ActionType Type => ActionType.Webhook;
    public override string DisplayName => "Webhook";
    public override string Description => $"{Method} {Url}";
    
    public string Url { get; set; } = string.Empty;
    public HttpMethodType Method { get; set; } = HttpMethodType.GET;
    public string Body { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/json";
    public Dictionary<string, string> Headers { get; set; } = new();
}
