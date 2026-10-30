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

    /// <summary>
    /// Filter Actor Standby runs by the pool that serves them: one or more of <c>SINGLE_TENANT</c>,
    /// <c>MULTI_TENANT_FREE_USERS</c>, <c>MULTI_TENANT_PAYING_USERS</c> or <c>MULTI_TENANT_OWNER_PAID</c>; sent
    /// as a comma-separated list. Requires <see cref="Status"/> with only non-terminal statuses (<c>READY</c>,
    /// <c>RUNNING</c>, <c>TIMING-OUT</c> or <c>ABORTING</c>). Only the account-wide and Actor-scoped run
    /// collections honour it.
    /// </summary>
    public IReadOnlyList<string>? Deployment { get; init; }

    /// <summary>Filter to runs started after this ISO-8601 timestamp.</summary>
    public string? StartedAfter { get; init; }

    /// <summary>Filter to runs started before this ISO-8601 timestamp.</summary>
    public string? StartedBefore { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddCsv("status", Status)
            .AddCsv("deployment", Deployment)
            .AddString("startedAfter", StartedAfter)
            .AddString("startedBefore", StartedBefore);
    }
}
