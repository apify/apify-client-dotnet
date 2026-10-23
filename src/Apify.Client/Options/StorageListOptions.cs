using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>
/// Options for the storage collection list endpoints (<c>GET /v2/datasets</c>,
/// <c>/v2/key-value-stores</c>, <c>/v2/request-queues</c>), which add <c>unnamed</c> and <c>ownership</c>
/// filters on top of the standard pagination.
/// </summary>
public sealed class StorageListOptions
{
    /// <summary>Number of items to skip from the beginning of the list.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of items to return.</summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Maximum number of items requested per API call when iterating across pages (must be positive). Only
    /// iteration uses it, as the page size; it is never sent to the API. The effective page size is the
    /// smaller of this and the remaining <c>Limit</c>. Defaults to the API's own page size.
    /// </summary>
    public int? ChunkSize { get; init; }

    /// <summary>If <c>true</c>, return items newest-first.</summary>
    public bool? Desc { get; init; }

    /// <summary>If <c>true</c>, include unnamed storages in the result.</summary>
    public bool? Unnamed { get; init; }

    /// <summary>Filter by ownership: <c>ownedByMe</c> or <c>sharedWithMe</c>.</summary>
    public string? Ownership { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("offset", Offset)
            .AddInt("limit", Limit)
            .AddBool("desc", Desc)
            .AddBool("unnamed", Unnamed)
            .AddString("ownership", Ownership);
    }
}
