using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>
/// The standard offset/limit pagination shared by most <c>list</c> endpoints (builds, runs, tasks,
/// schedules, webhooks, Actor versions). All fields are optional; leave one <c>null</c> to use the API
/// default.
/// </summary>
public sealed class ListOptions
{
    /// <summary>Number of items to skip from the beginning of the list.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of items to return.</summary>
    public int? Limit { get; init; }

    /// <summary>If <c>true</c>, return items newest-first.</summary>
    public bool? Desc { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("offset", Offset).AddInt("limit", Limit).AddBool("desc", Desc);
    }
}
