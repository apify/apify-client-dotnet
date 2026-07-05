using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>A request queue stores URLs to be crawled.</summary>
public sealed class RequestQueue : ApifyResource
{
    /// <summary>Wraps a raw request queue object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public RequestQueue(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique queue ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The queue name (empty for unnamed queues).</summary>
    public string? Name => GetString("name");

    /// <summary>The ID of the user who owns the queue.</summary>
    public string? UserId => GetString("userId");

    /// <summary>When the queue was created (ISO-8601 string).</summary>
    public string? CreatedAt => GetString("createdAt");

    /// <summary>When the queue was last modified (ISO-8601 string).</summary>
    public string? ModifiedAt => GetString("modifiedAt");

    /// <summary>The total number of requests ever added.</summary>
    public long? TotalRequestCount => GetInt("totalRequestCount");
}
