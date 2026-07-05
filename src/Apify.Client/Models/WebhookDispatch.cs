using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>A single invocation of a webhook.</summary>
public sealed class WebhookDispatch : ApifyResource
{
    /// <summary>Wraps a raw webhook dispatch object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public WebhookDispatch(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique dispatch ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The ID of the webhook that produced this dispatch.</summary>
    public string? WebhookId => GetString("webhookId");
}
