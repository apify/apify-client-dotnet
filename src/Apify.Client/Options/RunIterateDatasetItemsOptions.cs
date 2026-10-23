using System.Collections.Generic;

namespace Apify.Client.Options;

/// <summary>
/// Configures <see cref="Apify.Client.Resources.RunClient.IterateDatasetItemsAsync"/>, which iterates over
/// the items of a run's default dataset while the run is still producing them. Mirrors the reference
/// client's <c>RunIterateDatasetItemsOptions</c>. All fields are optional.
/// </summary>
public sealed class RunIterateDatasetItemsOptions
{
    /// <summary>Default maximum number of dataset rows requested per API call.</summary>
    public const int DefaultChunkSize = 1000;

    /// <summary>Default time, in seconds, to wait for the run to finish between polls.</summary>
    public const int DefaultPollIntervalSecs = 5;

    /// <summary>Number of dataset rows to skip from the beginning (defaults to 0).</summary>
    public int? Offset { get; init; }

    /// <summary>
    /// Maximum number of dataset rows to scan (<c>null</c> or <c>0</c> for no limit). Fewer items are
    /// yielded when filters drop some, more when <see cref="Unwind"/> splits a row into several.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Maximum number of dataset rows requested per API call (must be positive; defaults to
    /// <see cref="DefaultChunkSize"/>).
    /// </summary>
    public int? ChunkSize { get; init; }

    /// <summary>How long to wait for the run to finish between polls, in seconds (defaults to <see cref="DefaultPollIntervalSecs"/>).</summary>
    public int? PollIntervalSecs { get; init; }

    /// <summary>Return only non-empty items and skip hidden fields.</summary>
    public bool? Clean { get; init; }

    /// <summary>Restrict the output to these fields.</summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>Exclude these fields from the output.</summary>
    public IReadOnlyList<string>? Omit { get; init; }

    /// <summary>Expand these fields (each array element becomes a separate item).</summary>
    public IReadOnlyList<string>? Unwind { get; init; }

    /// <summary>Skip empty items.</summary>
    public bool? SkipEmpty { get; init; }

    /// <summary>Skip hidden fields (those starting with <c>#</c>).</summary>
    public bool? SkipHidden { get; init; }

    /// <summary>Whether any filter that can drop or split rows (<see cref="Clean"/>, <see cref="SkipEmpty"/>, <see cref="Unwind"/>) is set.</summary>
    internal bool HasRowFilters => Clean == true || SkipEmpty == true || Unwind is { Count: > 0 };
}
