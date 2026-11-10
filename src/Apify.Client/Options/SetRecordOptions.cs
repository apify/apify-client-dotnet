namespace Apify.Client.Options;

/// <summary>Write options of a key-value store record, as the reference client's <c>timeoutSecs</c> and <c>doNotRetryTimeouts</c>.</summary>
public sealed class SetRecordOptions
{
    /// <summary>Per-request timeout of the upload in seconds, capped at the client's overall request timeout.</summary>
    public int? TimeoutSecs { get; init; }

    /// <summary>If <c>true</c>, do not retry the upload when it fails with a request timeout.</summary>
    public bool DoNotRetryTimeouts { get; init; }

    /// <summary>
    /// The encoding already applied to the value, sent as the <c>Content-Encoding</c> header, to upload a pre-compressed value
    /// (<c>gzip</c>, <c>br</c>, <c>zstd</c>, <c>deflate</c> or <c>identity</c>). The client sends the bytes as they are and
    /// skips its own compression. Must not be empty or whitespace-only.
    /// </summary>
    public string? ContentEncoding { get; init; }
}
