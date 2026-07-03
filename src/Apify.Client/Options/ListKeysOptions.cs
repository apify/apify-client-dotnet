using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures listing keys in a key-value store.</summary>
public sealed class ListKeysOptions
{
    /// <summary>Maximum number of keys to return.</summary>
    public int? Limit { get; init; }

    /// <summary>List keys after this one (for pagination).</summary>
    public string? ExclusiveStartKey { get; init; }

    /// <summary>Restrict the listing to keys with this prefix.</summary>
    public string? Prefix { get; init; }

    /// <summary>Restrict the listing to a named collection of keys.</summary>
    public string? Collection { get; init; }

    /// <summary>A pre-shared URL signature granting access without an API token.</summary>
    public string? Signature { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("limit", Limit)
            .AddString("exclusiveStartKey", ExclusiveStartKey)
            .AddString("prefix", Prefix)
            .AddString("collection", Collection)
            .AddString("signature", Signature);
    }
}
