using System.Collections.Generic;
using System.Linq;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Options;

/// <summary>
/// Run-specific filters for listing runs. The <c>StartedAfter</c>/<c>StartedBefore</c> filters are only
/// honoured by the Actor-scoped and task-scoped run collections.
/// </summary>
public sealed class RunListOptions
{
    /// <summary>
    /// Filter by one or more run statuses; sent as a comma-separated list. Leave <c>null</c> to not filter
    /// by status.
    /// </summary>
    public IReadOnlyList<ActorJobStatus>? Status { get; init; }

    /// <summary>Filter to runs started after this ISO-8601 timestamp.</summary>
    public string? StartedAfter { get; init; }

    /// <summary>Filter to runs started before this ISO-8601 timestamp.</summary>
    public string? StartedBefore { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddCsv("status", Status?.Select(s => s.ToWireValue()).ToList())
            .AddString("startedAfter", StartedAfter)
            .AddString("startedBefore", StartedBefore);
    }
}
