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
    /// <param name="timeoutSecs">
    /// Overall per-request timeout budget, in seconds: the cap every attempt's growing timeout is clamped
    /// to, and the base duration of the <see cref="RequestTier.Long"/> tier (the reference client's
    /// <c>timeoutLongSecs</c> and <c>timeoutMaxSecs</c> default to the same value, 360s, for the same reason).
    /// </param>
    /// <param name="shortTimeoutSecs">Base duration of the <see cref="RequestTier.Short"/> tier, in seconds.</param>
    /// <param name="mediumTimeoutSecs">Base duration of the <see cref="RequestTier.Medium"/> tier, in seconds.</param>
    public RetryConfig(int maxRetries, double minDelayMillis, double maxDelayMillis, double timeoutSecs, double shortTimeoutSecs, double mediumTimeoutSecs)
    {
        MaxRetries = maxRetries;
        MinDelayMillis = minDelayMillis;
        MaxDelayMillis = maxDelayMillis;
        TimeoutSecs = timeoutSecs;
        ShortTimeoutSecs = shortTimeoutSecs;
        MediumTimeoutSecs = mediumTimeoutSecs;
    }

    /// <summary>Maximum number of retries (the request is attempted up to <c>MaxRetries + 1</c> times).</summary>
    public int MaxRetries { get; }

    /// <summary>Minimum delay between retries, in milliseconds; doubled on each retry (exponential backoff).</summary>
    public double MinDelayMillis { get; }

    /// <summary>Upper bound on the (exponentially growing) inter-retry delay, in milliseconds.</summary>
    public double MaxDelayMillis { get; }

    /// <summary>
    /// Overall per-request timeout budget, in seconds (every attempt's growing timeout is capped here,
    /// regardless of tier); also the base duration of the <see cref="RequestTier.Long"/> tier.
    /// </summary>
    public double TimeoutSecs { get; }

    /// <summary>Base duration of the <see cref="RequestTier.Short"/> tier, in seconds.</summary>
    public double ShortTimeoutSecs { get; }

    /// <summary>Base duration of the <see cref="RequestTier.Medium"/> tier, in seconds.</summary>
    public double MediumTimeoutSecs { get; }

    /// <summary>Resolves a tier to its configured base duration, in seconds.</summary>
    public double TierSecs(RequestTier tier) => tier switch
    {
        RequestTier.Short => ShortTimeoutSecs,
        RequestTier.Medium => MediumTimeoutSecs,
        _ => TimeoutSecs,
    };
}
