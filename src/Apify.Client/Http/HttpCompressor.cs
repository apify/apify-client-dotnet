using System.Threading;
using System.Threading.Tasks;

namespace Apify.Client.Http;

/// <summary>
/// Strategy for compressing HTTP request bodies.
/// </summary>
/// <remarks>
/// Derive from this class to plug in a custom compressor through <see cref="ApifyClientOptions.Compressor"/>:
/// set <see cref="ContentEncoding"/> to the value sent in the <c>Content-Encoding</c> header and implement
/// <see cref="CompressAsync"/>. The client calls it only for bodies that are large enough to benefit from
/// compression and whose content type does not already carry its own compression, and never for a body the
/// caller already sent with a <c>Content-Encoding</c> of its own. The Apify API accepts the <c>gzip</c>,
/// <c>br</c>, <c>deflate</c> and <c>identity</c> encodings. Matches the reference client's <c>HttpCompressor</c>.
/// </remarks>
public abstract class HttpCompressor
{
    /// <summary>The value sent in the <c>Content-Encoding</c> header, for example <c>gzip</c> or <c>br</c>.</summary>
    public abstract string ContentEncoding { get; }

    /// <summary>Compresses a request body.</summary>
    /// <param name="data">The raw bytes to compress.</param>
    /// <param name="cancellationToken">A token to cancel the compression.</param>
    /// <returns>The compressed bytes.</returns>
    public abstract Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default);
}
