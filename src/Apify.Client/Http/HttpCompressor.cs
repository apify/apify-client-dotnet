using System.Threading;
using System.Threading.Tasks;

namespace Apify.Client.Http;

/// <summary>Strategy for compressing HTTP request bodies.</summary>
/// <remarks>
/// Derive from this class and pass it as <see cref="ApifyClientOptions.Compressor"/>: set <see cref="ContentEncoding"/>
/// to the <c>Content-Encoding</c> value and implement <see cref="CompressAsync"/>. The client calls it only for large
/// bodies whose content type has no compression of its own, and never for a body the caller sent with a
/// <c>Content-Encoding</c>. The API accepts <c>gzip</c>, <c>br</c>, <c>zstd</c>, <c>deflate</c>, and <c>identity</c>.
/// Matches the reference client's <c>HttpCompressor</c>.
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
