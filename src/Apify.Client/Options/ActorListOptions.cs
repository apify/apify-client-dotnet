using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Options for listing the account's Actors.</summary>
public sealed class ActorListOptions
{
    /// <summary>Number of Actors to skip.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of Actors to return.</summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Maximum items requested per API call when iterating, which must be positive. It sizes the pages and is never sent to
    /// the API; the page size is the smaller of this and the remaining <c>Limit</c>. Defaults to the API's page size.
    /// </summary>
    public int? ChunkSize { get; init; }

    /// <summary>If <c>true</c>, return Actors newest-first.</summary>
    public bool? Desc { get; init; }

    /// <summary>If <c>true</c>, return only Actors owned by the current user.</summary>
    public bool? My { get; init; }

    /// <summary>The sort field (e.g. <c>createdAt</c>, <c>stats.lastRunStartedAt</c>).</summary>
    public string? SortBy { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("offset", Offset)
            .AddInt("limit", Limit)
            .AddBool("desc", Desc)
            .AddBool("my", My)
            .AddString("sortBy", SortBy);
    }
}
