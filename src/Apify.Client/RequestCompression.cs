namespace Apify.Client;

/// <summary>Algorithm used to compress large request bodies.</summary>
/// <remarks>
/// Mirrors the reference client's <c>compression</c> option, which defaults to brotli. Only string or byte payloads of
/// at least 1024 bytes are compressed.
/// </remarks>
public enum RequestCompression
{
    /// <summary>Compress with brotli (<c>Content-Encoding: br</c>). This is the default.</summary>
    Brotli,

    /// <summary>Compress with gzip (<c>Content-Encoding: gzip</c>).</summary>
    Gzip,

    /// <summary>Compress with zstd (<c>Content-Encoding: zstd</c>).</summary>
    Zstd,
}
