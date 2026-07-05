namespace Apify.Client.Options;

/// <summary>
/// Write options for storing a key-value-store record, mirroring the reference client's
/// <c>timeoutSecs</c>/<c>doNotRetryTimeouts</c>.
/// </summary>
public sealed class SetRecordOptions
{
    /// <summary>
    /// Per-request timeout for the upload, in seconds. Use it to shorten the wait for this upload; defaults
    /// to (and is capped at) the client's configured overall request timeout, so a value larger than that
    /// timeout has no effect.
    /// </summary>
    public int? TimeoutSecs { get; init; }

    /// <summary>If <c>true</c>, do not retry the upload when it fails with a request timeout.</summary>
    public bool DoNotRetryTimeouts { get; init; }
}
