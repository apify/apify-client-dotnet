namespace Apify.Client;

/// <summary>
/// Algorithm used to compress large request bodies before sending them.
/// </summary>
/// <remarks>
/// Mirrors the reference client's explicit <c>compression: 'brotli' | 'gzip'</c> option, which defaults to
/// brotli. Regardless of the choice, only string/byte payloads at or above the compression threshold are
/// compressed.
/// </remarks>
public enum RequestCompression
{
    /// <summary>Compress with brotli (<c>Content-Encoding: br</c>). This is the default and matches the reference client's preference.</summary>
    Brotli,

    /// <summary>Compress with gzip (<c>Content-Encoding: gzip</c>).</summary>
    Gzip,
}
