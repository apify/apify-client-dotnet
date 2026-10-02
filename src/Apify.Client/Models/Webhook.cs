using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>A webhook notifies an external service when specific events occur.</summary>
public sealed class Webhook : ApifyResource
{
    /// <summary>Wraps a raw webhook object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public Webhook(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique webhook ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The ID of the user who owns the webhook.</summary>
    public string? UserId => GetString("userId");

    /// <summary>
    /// The URL the webhook posts to. Normalized to its RFC 3986 absolute-URI form (see
    /// <see cref="ApifyResource.GetUrlString"/>), matching the reference client.
    /// </summary>
    public string? RequestUrl => GetUrlString("requestUrl");

    /// <summary>The events that trigger the webhook.</summary>
    public IReadOnlyList<string>? EventTypes => GetStringList("eventTypes");
}
