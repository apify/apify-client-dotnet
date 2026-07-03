using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>
/// A single request stored in a request queue. Fields left <c>null</c> are omitted when the request is
/// sent to the API. Construct one for adding to a queue, or receive one when reading a queue.
/// </summary>
public sealed class RequestQueueRequest : ApifyResource
{
    private RequestQueueRequest(JsonObject data)
        : base(data)
    {
    }

    /// <summary>Creates a request, optionally with a URL and unique (deduplication) key.</summary>
    /// <param name="url">The request URL.</param>
    /// <param name="uniqueKey">The deduplication key for the request.</param>
    public RequestQueueRequest(string? url = null, string? uniqueKey = null)
        : this(new JsonObject())
    {
        if (url is not null)
        {
            Url = url;
        }

        if (uniqueKey is not null)
        {
            UniqueKey = uniqueKey;
        }
    }

    /// <summary>Wraps a raw request object (used when hydrating from the API).</summary>
    /// <param name="data">The raw decoded request object.</param>
    public static RequestQueueRequest FromJsonObject(JsonObject data) => new(data);

    /// <summary>The unique request ID (assigned by the API; absent on create).</summary>
    public string? Id
    {
        get => GetString("id");
        set => SetString("id", value);
    }

    /// <summary>The request URL.</summary>
    public string? Url
    {
        get => GetString("url");
        set => SetString("url", value);
    }

    /// <summary>The deduplication key for the request.</summary>
    public string? UniqueKey
    {
        get => GetString("uniqueKey");
        set => SetString("uniqueKey", value);
    }

    /// <summary>The HTTP method (e.g. <c>GET</c>, <c>POST</c>).</summary>
    public string? Method
    {
        get => GetString("method");
        set => SetString("method", value);
    }

    /// <summary>Arbitrary user-attached metadata.</summary>
    public JsonNode? UserData
    {
        get => Get("userData");
        set
        {
            if (value is null)
            {
                ToJsonObject().Remove("userData");
            }
            else
            {
                ToJsonObject()["userData"] = value.DeepClone();
            }
        }
    }

    private void SetString(string key, string? value)
    {
        if (value is null)
        {
            ToJsonObject().Remove(key);
        }
        else
        {
            ToJsonObject()[key] = value;
        }
    }
}
