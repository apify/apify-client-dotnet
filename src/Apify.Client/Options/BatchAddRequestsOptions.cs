using System;

namespace Apify.Client.Options;

/// <summary>
/// Tuning options for batch request adding, mirroring the reference client. Requests the API reports as
/// unprocessed (typically due to rate limiting) are automatically retried with exponential backoff.
/// </summary>
public sealed class BatchAddRequestsOptions
{
    /// <summary>Default number of retry rounds for unprocessed requests (matches the reference client).</summary>
    public const int DefaultMaxUnprocessedRetries = 3;

    /// <summary>Default maximum number of batch API calls made in parallel (matches the reference client).</summary>
    public const int DefaultMaxParallel = 5;

    /// <summary>Default minimum delay before retrying unprocessed requests (matches the reference client).</summary>
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
