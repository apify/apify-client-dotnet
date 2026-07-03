using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>A single key listed from a key-value store.</summary>
public sealed class KeyValueStoreKey : ApifyResource
{
    /// <summary>Wraps a raw key object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public KeyValueStoreKey(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The record key.</summary>
    public string? Key => GetString("key");

    /// <summary>The record size in bytes.</summary>
    public long? Size => GetInt("size");
}
