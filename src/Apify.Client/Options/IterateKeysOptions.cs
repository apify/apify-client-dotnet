namespace Apify.Client.Options;

/// <summary>Configures <see cref="Apify.Client.Resources.KeyValueStoreClient.IterateKeysAsync"/>, as the reference client's chunked <c>listKeys()</c>.</summary>
public sealed class IterateKeysOptions
{
    /// <summary>Default maximum number of keys fetched per page (matches the API's own default/maximum).</summary>
    public const int DefaultMaxPageLimit = 1000;

    /// <summary>Maximum total number of keys to iterate across all pages (<c>null</c> for no bound).</summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Maximum keys requested per API call, which must be positive (default <see cref="DefaultMaxPageLimit"/>). It only
    /// sizes the pages and is never sent to the API. As the reference client's <c>chunkSize</c>.
    /// </summary>
    public int? ChunkSize { get; init; }

    /// <summary>The earlier name of <see cref="ChunkSize"/>; <see cref="ChunkSize"/> wins when both are set.</summary>
    public int? MaxPageLimit { get; init; }

    /// <summary>Start iterating after this key (first page only).</summary>
    public string? ExclusiveStartKey { get; init; }

    /// <summary>Filter keys by collection name.</summary>
    public string? Collection { get; init; }

    /// <summary>Only iterate keys with this prefix.</summary>
    public string? Prefix { get; init; }
}
