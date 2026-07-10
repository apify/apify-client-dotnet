namespace Apify.Client;

/// <summary>
/// Algorithm used to compress large request bodies before sending them.
/// </summary>
/// <remarks>
/// The reference client prefers brotli and only falls back to gzip on runtimes where brotli is
/// unavailable. .NET always ships brotli, so instead of an automatic fallback this option lets callers
/// choose the algorithm explicitly, keeping both code paths genuinely selectable. Regardless of the
/// choice, only string/byte payloads at or above the compression threshold are compressed.
/// </remarks>
public enum RequestCompression
{
    /// <summary>Compress with brotli (<c>Content-Encoding: br</c>). This is the default and matches the reference client's preference.</summary>
    Brotli,

    /// <summary>Compress with gzip (<c>Content-Encoding: gzip</c>).</summary>
    Gzip,
}
