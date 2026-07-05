using System.Collections.Generic;
using System.Text.Json.Nodes;
using Apify.Client.Internal;

namespace Apify.Client.Models;

/// <summary>A page of keys from a key-value store.</summary>
public sealed class KeyValueStoreKeysPage
{
    private KeyValueStoreKeysPage(
        IReadOnlyList<KeyValueStoreKey> items,
        long limit,
        bool isTruncated,
        string? exclusiveStartKey,
        string? nextExclusiveStartKey)
    {
        Items = items;
        Limit = limit;
        IsTruncated = isTruncated;
        ExclusiveStartKey = exclusiveStartKey;
        NextExclusiveStartKey = nextExclusiveStartKey;
    }

    /// <summary>Builds a page from the decoded keys-page object.</summary>
    /// <param name="data">The decoded keys-page object.</param>
    public static KeyValueStoreKeysPage FromData(JsonNode? data)
    {
        var obj = JsonValues.AsObject(data);
        var items = new List<KeyValueStoreKey>();
        foreach (var item in JsonValues.ObjectItems(obj))
        {
            items.Add(new KeyValueStoreKey(item));
        }

        return new KeyValueStoreKeysPage(
            items,
            JsonValues.IntOr(obj, "limit", items.Count),
            JsonValues.BoolOr(obj, "isTruncated", false),
            JsonValues.String(obj, "exclusiveStartKey"),
            JsonValues.String(obj, "nextExclusiveStartKey"));
    }

    /// <summary>The listed keys.</summary>
    public IReadOnlyList<KeyValueStoreKey> Items { get; }

    /// <summary>The maximum number of keys requested.</summary>
    public long Limit { get; }

    /// <summary>Whether more keys are available.</summary>
    public bool IsTruncated { get; }

    /// <summary>The key the listing started after.</summary>
    public string? ExclusiveStartKey { get; }

    /// <summary>The key to pass to fetch the next page.</summary>
    public string? NextExclusiveStartKey { get; }
}
