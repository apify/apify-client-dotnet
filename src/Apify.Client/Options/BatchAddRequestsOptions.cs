using System;

namespace Apify.Client.Options;

/// <summary>Tuning options of batch request adding. Requests the API reports as unprocessed are retried with exponential backoff.</summary>
public sealed class BatchAddRequestsOptions
{
    /// <summary>Default number of retry rounds for unprocessed requests, as in the reference client.</summary>
    public const int DefaultMaxUnprocessedRetries = 3;

    /// <summary>Default maximum number of parallel batch API calls, as in the reference client.</summary>
    public const int DefaultMaxParallel = 5;

    /// <summary>Default minimum delay before retrying unprocessed requests, as in the reference client.</summary>
    public const int DefaultMinDelayMillis = 500;

    /// <summary>Creates batch-add options, clamping values to their valid ranges.</summary>
    /// <param name="maxUnprocessedRequestsRetries">Number of retry rounds for unprocessed requests.</param>
    /// <param name="maxParallel">Maximum number of batch API calls made in parallel.</param>
    /// <param name="minDelayBetweenUnprocessedRequestsRetriesMillis">Minimum delay before retrying unprocessed requests.</param>
    public BatchAddRequestsOptions(
        int maxUnprocessedRequestsRetries = DefaultMaxUnprocessedRetries,
        int maxParallel = DefaultMaxParallel,
        int minDelayBetweenUnprocessedRequestsRetriesMillis = DefaultMinDelayMillis)
    {
        MaxUnprocessedRequestsRetries = Math.Max(0, maxUnprocessedRequestsRetries);
        MaxParallel = Math.Max(1, maxParallel);
        MinDelayBetweenUnprocessedRequestsRetriesMillis = Math.Max(0, minDelayBetweenUnprocessedRequestsRetriesMillis);
    }

    /// <summary>Number of retry rounds for requests the API reports as unprocessed.</summary>
    public int MaxUnprocessedRequestsRetries { get; }

    /// <summary>Maximum number of batch API calls made in parallel.</summary>
    public int MaxParallel { get; }

    /// <summary>Minimum delay before retrying unprocessed requests, in milliseconds.</summary>
    public int MinDelayBetweenUnprocessedRequestsRetriesMillis { get; }
}
