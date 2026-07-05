using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>A key-value store holds arbitrary data records.</summary>
public sealed class KeyValueStore : ApifyResource
{
    /// <summary>Wraps a raw key-value store object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public KeyValueStore(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique store ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The store name (empty for unnamed stores).</summary>
    public string? Name => GetString("name");

    /// <summary>The ID of the user who owns the store.</summary>
    public string? UserId => GetString("userId");

    /// <summary>When the store was created (ISO-8601 string).</summary>
    public string? CreatedAt => GetString("createdAt");

    /// <summary>When the store was last modified (ISO-8601 string).</summary>
    public string? ModifiedAt => GetString("modifiedAt");
}
