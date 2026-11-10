using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>
/// The offset/limit pagination shared by the list endpoints (builds, runs, tasks, schedules, webhooks, Actor versions).
/// All fields are optional; <c>null</c> uses the API default.
/// </summary>
public sealed class ListOptions
{
    /// <summary>Number of items to skip from the beginning of the list.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of items to return.</summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Maximum items requested per API call when iterating, which must be positive. It sizes the pages and is never sent to
    /// the API; the page size is the smaller of this and the remaining <c>Limit</c>. Defaults to the API's page size.
    /// </summary>
    public int? ChunkSize { get; init; }

    /// <summary>If <c>true</c>, return items newest-first.</summary>
    public bool? Desc { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("offset", Offset).AddInt("limit", Limit).AddBool("desc", Desc);
    }
}
