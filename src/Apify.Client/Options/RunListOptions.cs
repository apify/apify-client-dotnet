using System.Collections.Generic;
using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>
/// Run-specific filters for listing runs. The <c>StartedAfter</c>/<c>StartedBefore</c> filters are only
/// honoured by the Actor-scoped and task-scoped run collections.
/// </summary>
public sealed class RunListOptions
{
    /// <summary>
    /// Filter by one or more run statuses (e.g. <c>SUCCEEDED</c>, <c>RUNNING</c>); sent as a
    /// comma-separated list.
    /// </summary>
    public IReadOnlyList<string>? Status { get; init; }

    /// <summary>Filter to runs started after this ISO-8601 timestamp.</summary>
    public string? StartedAfter { get; init; }

    /// <summary>Filter to runs started before this ISO-8601 timestamp.</summary>
    public string? StartedBefore { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddCsv("status", Status)
            .AddString("startedAfter", StartedAfter)
            .AddString("startedBefore", StartedBefore);
    }
}
