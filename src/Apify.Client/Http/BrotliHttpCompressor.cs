using System;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Apify.Client.Http;

/// <summary>
/// Compresses request bodies with brotli (<c>Content-Encoding: br</c>). Matches the reference client's
/// <c>BrotliHttpCompressor</c>.
/// </summary>
public sealed class BrotliHttpCompressor : HttpCompressor
{
    /// <summary>The lowest valid brotli quality: the fastest, with the least compression.</summary>
    public const int MinQuality = 0;

    /// <summary>The highest valid brotli quality: the slowest, with the best compression.</summary>
    public const int MaxQuality = 11;

    /// <summary>The default quality, the middle of the range where brotli already beats gzip at a comparable CPU cost.</summary>
    public const int DefaultQuality = 6;

    /// <summary>The brotli sliding window size, as the base-2 logarithm of its size in bytes (the library default).</summary>
    private const int WindowBits = 22;

    private readonly int _quality;

    /// <summary>Creates a brotli compressor.</summary>
    /// <param name="quality">The compression level, from <see cref="MinQuality"/> (the fastest) to <see cref="MaxQuality"/> (the best compression).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quality"/> is outside the valid range.</exception>
    public BrotliHttpCompressor(int quality = DefaultQuality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, MinQuality);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, MaxQuality);
        _quality = quality;
    }

    /// <inheritdoc />
    public override string ContentEncoding => "br";

    /// <inheritdoc />
    public override Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default)
        => Task.Run(() => Compress(data), cancellationToken);

    private byte[] Compress(byte[] data)
    {
        var buffer = new byte[BrotliEncoder.GetMaxCompressedLength(data.Length)];
        if (!BrotliEncoder.TryCompress(data, buffer, out var written, _quality, WindowBits))
        {
            throw new InvalidOperationException("brotli compression failed");
        }

        return buffer.AsSpan(0, written).ToArray();
    }
}
