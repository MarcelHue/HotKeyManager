using System.Net.Http;
using System.Text;
using HotKeyManager.Models;

namespace HotKeyManager.Services.Execution;

/// <summary>Sends the HTTP request of a webhook action.</summary>
internal sealed class WebhookSender
{
    private readonly HttpClient httpClient = new();

    public async Task SendAsync(WebhookAction action)
    {
        using var request = new HttpRequestMessage(ToHttpMethod(action.Method), new Uri(action.Url));
        foreach (var header in action.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var methodHasBody = action.Method is HttpMethodType.POST or HttpMethodType.PUT or HttpMethodType.PATCH;
        if (methodHasBody && !string.IsNullOrEmpty(action.Body))
        {
            request.Content = new StringContent(action.Body, Encoding.UTF8, action.ContentType);
        }

        using var response = await this.httpClient.SendAsync(request);
    }

    private static HttpMethod ToHttpMethod(HttpMethodType method) => method switch
    {
        HttpMethodType.POST => HttpMethod.Post,
        HttpMethodType.PUT => HttpMethod.Put,
        HttpMethodType.DELETE => HttpMethod.Delete,
        HttpMethodType.PATCH => HttpMethod.Patch,
        _ => HttpMethod.Get,
    };
}
