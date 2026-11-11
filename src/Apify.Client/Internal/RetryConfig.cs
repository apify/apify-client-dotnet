namespace Apify.Client.Internal;

/// <summary>Retry and timeout policy of the HTTP client.</summary>
internal sealed class RetryConfig
{
    /// <summary>Creates a retry policy.</summary>
    /// <param name="maxRetries">Maximum retries; a request is attempted up to <c>maxRetries + 1</c> times.</param>
    /// <param name="minDelayMillis">Minimum delay between retries in ms, doubled on each retry.</param>
    /// <param name="maxDelayMillis">Upper bound of the growing delay in ms.</param>
    /// <param name="timeoutSecs">Overall per-request timeout in seconds: the cap of every attempt's timeout (the reference client's <c>timeoutMaxSecs</c>).</param>
    /// <param name="longTimeoutSecs">Base duration of the <see cref="RequestTier.Long"/> tier, in seconds.</param>
    /// <param name="shortTimeoutSecs">Base duration of the <see cref="RequestTier.Short"/> tier, in seconds.</param>
    /// <param name="mediumTimeoutSecs">Base duration of the <see cref="RequestTier.Medium"/> tier, in seconds.</param>
    public RetryConfig(int maxRetries, double minDelayMillis, double maxDelayMillis, double timeoutSecs, double longTimeoutSecs, double shortTimeoutSecs, double mediumTimeoutSecs)
    {
        MaxRetries = maxRetries;
        MinDelayMillis = minDelayMillis;
        MaxDelayMillis = maxDelayMillis;
        TimeoutSecs = timeoutSecs;
        LongTimeoutSecs = longTimeoutSecs;
        ShortTimeoutSecs = shortTimeoutSecs;
        MediumTimeoutSecs = mediumTimeoutSecs;
    }

    /// <summary>Maximum number of retries.</summary>
    public int MaxRetries { get; }

    /// <summary>Minimum delay between retries in ms, doubled on each retry.</summary>
    public double MinDelayMillis { get; }

    /// <summary>Upper bound of the growing delay between retries in ms.</summary>
    public double MaxDelayMillis { get; }

    /// <summary>Overall per-request timeout in seconds: the cap of every attempt's timeout, whatever the tier.</summary>
    public double TimeoutSecs { get; }

    /// <summary>Base duration of the <see cref="RequestTier.Long"/> tier, in seconds.</summary>
    public double LongTimeoutSecs { get; }

    /// <summary>Base duration of the <see cref="RequestTier.Short"/> tier, in seconds.</summary>
    public double ShortTimeoutSecs { get; }

    /// <summary>Base duration of the <see cref="RequestTier.Medium"/> tier, in seconds.</summary>
    public double MediumTimeoutSecs { get; }

    /// <summary>Resolves a tier to its configured base duration, in seconds.</summary>
    public double TierSecs(RequestTier tier) => tier switch
    {
        RequestTier.Short => ShortTimeoutSecs,
        RequestTier.Medium => MediumTimeoutSecs,
        _ => LongTimeoutSecs,
    };
}
