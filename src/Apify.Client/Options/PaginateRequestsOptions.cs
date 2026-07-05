using System;
using System.Collections.Generic;
using System.Globalization;

namespace Apify.Client.Options;

/// <summary>
/// Configures lazy iteration over a request queue's requests
/// (<see cref="Apify.Client.Resources.RequestQueueClient.PaginateRequestsAsync"/>), mirroring the
/// reference client's <c>paginateRequests({ limit, maxPageLimit, exclusiveStartId, cursor, filter })</c>.
/// </summary>
public sealed class PaginateRequestsOptions
{
    /// <summary>Default maximum number of requests fetched per page (matches the reference client).</summary>
    public const int DefaultMaxPageLimit = 1000;

    /// <summary>Filter value: currently locked requests.</summary>
    public const string FilterLocked = "locked";

    /// <summary>Filter value: pending (not-yet-handled) requests.</summary>
    public const string FilterPending = "pending";

    /// <summary>Maximum total number of requests to iterate across all pages (<c>null</c> for no bound).</summary>
    public int? Limit { get; init; }

    /// <summary>Maximum number of requests fetched per page (defaults to <see cref="DefaultMaxPageLimit"/>).</summary>
    public int? MaxPageLimit { get; init; }

    /// <summary>Start iterating after this request ID (first page only; mutually exclusive with cursor).</summary>
    public string? ExclusiveStartId { get; init; }

    /// <summary>An opaque pagination cursor to start from (mutually exclusive with <see cref="ExclusiveStartId"/>).</summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Restrict the iteration to requests in the given states; each value must be
    /// <see cref="FilterLocked"/> or <see cref="FilterPending"/>.
    /// </summary>
    public IReadOnlyList<string>? Filter { get; init; }

    /// <summary>Validates the options for API-level constraints.</summary>
    internal void Validate()
    {
        if (ExclusiveStartId is not null && Cursor is not null)
        {
            throw new ArgumentException("PaginateRequestsOptions: ExclusiveStartId and Cursor are mutually exclusive");
        }

        if (Filter is not null)
        {
            foreach (var f in Filter)
            {
                if (f != FilterLocked && f != FilterPending)
                {
                    throw new ArgumentException(string.Format(
                        CultureInfo.InvariantCulture,
                        "PaginateRequestsOptions: filter entries must be \"{0}\" or \"{1}\", got \"{2}\"",
                        FilterLocked,
                        FilterPending,
                        f));
                }
            }
        }
    }
}
