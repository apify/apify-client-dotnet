namespace Apify.Client.Internal;

/// <summary>
/// Retry/timeout policy for the orchestrating HTTP client.
/// </summary>
internal sealed class RetryConfig
{
    /// <summary>Creates a retry policy.</summary>
    /// <param name="maxRetries">Maximum retries (the request is attempted up to <c>maxRetries + 1</c> times).</param>
    /// <param name="minDelayMillis">Minimum delay between retries, in ms; doubled on each retry.</param>
    /// <param name="maxDelayMillis">Upper bound on the (exponentially growing) inter-retry delay, in ms.</param>
    /// <param name="timeoutSecs">Overall per-request timeout budget, in seconds.</param>
    public RetryConfig(int maxRetries, double minDelayMillis, double maxDelayMillis, double timeoutSecs)
    {
        MaxRetries = maxRetries;
        MinDelayMillis = minDelayMillis;
        MaxDelayMillis = maxDelayMillis;
        TimeoutSecs = timeoutSecs;
    }

    /// <summary>Maximum number of retries (the request is attempted up to <c>MaxRetries + 1</c> times).</summary>
    public int MaxRetries { get; }

    /// <summary>Minimum delay between retries, in milliseconds; doubled on each retry (exponential backoff).</summary>
    public double MinDelayMillis { get; }

    /// <summary>Upper bound on the (exponentially growing) inter-retry delay, in milliseconds.</summary>
    public double MaxDelayMillis { get; }

    /// <summary>Overall per-request timeout budget, in seconds. Each attempt's timeout grows but is capped here.</summary>
    public double TimeoutSecs { get; }
}
