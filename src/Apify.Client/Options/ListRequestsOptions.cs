using System;
using System.Collections.Generic;
using System.Globalization;
using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures listing a request queue's requests.</summary>
public sealed class ListRequestsOptions
{
    /// <summary>Filter value: currently locked requests.</summary>
    public const string FilterLocked = "locked";

    /// <summary>Filter value: pending (not-yet-handled) requests.</summary>
    public const string FilterPending = "pending";

    /// <summary>Maximum number of requests to return.</summary>
    public int? Limit { get; init; }

    /// <summary>List requests after this ID.</summary>
    public string? ExclusiveStartId { get; init; }

    /// <summary>An opaque pagination cursor (alternative to <see cref="ExclusiveStartId"/>).</summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// Restrict the listing to requests in the given states; each value must be <see cref="FilterLocked"/>
    /// or <see cref="FilterPending"/>.
    /// </summary>
    public IReadOnlyList<string>? Filter { get; init; }

    /// <summary>Validates the options for API-level constraints.</summary>
    internal void Validate()
    {
        if (ExclusiveStartId is not null && Cursor is not null)
        {
            throw new ArgumentException("ListRequestsOptions: ExclusiveStartId and Cursor are mutually exclusive");
        }

        if (Filter is not null)
        {
            foreach (var f in Filter)
            {
                if (f != FilterLocked && f != FilterPending)
                {
                    throw new ArgumentException(string.Format(
                        CultureInfo.InvariantCulture,
                        "ListRequestsOptions: filter entries must be \"{0}\" or \"{1}\", got \"{2}\"",
                        FilterLocked,
                        FilterPending,
                        f));
                }
            }
        }
    }

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("limit", Limit)
            .AddString("exclusiveStartId", ExclusiveStartId)
            .AddString("cursor", Cursor)
            .AddCsv("filter", Filter);
    }
}
