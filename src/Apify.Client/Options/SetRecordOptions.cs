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

    /// <summary>
    /// Encoding already applied to the record value, sent verbatim as the <c>Content-Encoding</c> header.
    /// Set it to upload a pre-compressed value (<c>gzip</c>, <c>br</c>, <c>deflate</c>, or <c>identity</c>):
    /// the client then sends the bytes as they are and skips its own compression, so they are never
    /// compressed twice. Must not be empty or whitespace-only.
    /// </summary>
    public string? ContentEncoding { get; init; }
}
