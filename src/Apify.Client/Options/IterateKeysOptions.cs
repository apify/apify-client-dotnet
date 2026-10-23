namespace Apify.Client.Options;

/// <summary>
/// Configures lazy iteration over a key-value store's keys
/// (<see cref="Apify.Client.Resources.KeyValueStoreClient.IterateKeysAsync"/>), mirroring the reference
/// client's chunked <c>listKeys()</c> iteration (<c>limit</c>, <c>chunkSize</c>, ...).
/// </summary>
public sealed class IterateKeysOptions
{
    /// <summary>Default maximum number of keys fetched per page (matches the API's own default/maximum).</summary>
    public const int DefaultMaxPageLimit = 1000;

    /// <summary>Maximum total number of keys to iterate across all pages (<c>null</c> for no bound).</summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Maximum number of keys requested per API call (must be positive; defaults to
    /// <see cref="DefaultMaxPageLimit"/>). Never sent to the API as-is; it only sizes each page request.
    /// Matches the reference client's <c>chunkSize</c>.
    /// </summary>
    public int? ChunkSize { get; init; }

    /// <summary>
    /// The earlier name of <see cref="ChunkSize"/>, kept so existing callers keep working. <see cref="ChunkSize"/>
    /// takes precedence when both are set.
    /// </summary>
    public int? MaxPageLimit { get; init; }

    /// <summary>Start iterating after this key (first page only).</summary>
    public string? ExclusiveStartKey { get; init; }

    /// <summary>Filter keys by collection name.</summary>
    public string? Collection { get; init; }

    /// <summary>Only iterate keys with this prefix.</summary>
    public string? Prefix { get; init; }
}
