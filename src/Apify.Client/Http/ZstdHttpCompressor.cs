using System;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp;

namespace Apify.Client.Http;

/// <summary>
/// Compresses request bodies with zstd (<c>Content-Encoding: zstd</c>). Matches the reference client's
/// <c>ZstdHttpCompressor</c>.
/// </summary>
public sealed class ZstdHttpCompressor : HttpCompressor
{
    /// <summary>The lowest valid zstd quality: the fastest, with the least compression.</summary>
    public const int MinQuality = 1;

    /// <summary>
    /// The highest valid zstd quality. zstd itself goes up to 22, but levels 20 to 22 use a window above the 8 MB
    /// that RFC 9659 requires HTTP decoders to support, so a record stored at those levels could fail to decode.
    /// </summary>
    public const int MaxQuality = 19;

    /// <summary>zstd's own default: fast, with a ratio close to the best gzip level.</summary>
    public const int DefaultQuality = 3;

    private readonly int _quality;

    /// <summary>Creates a zstd compressor.</summary>
    /// <param name="quality">The compression level, from <see cref="MinQuality"/> (the fastest) to <see cref="MaxQuality"/> (the best compression).</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="quality"/> is outside the valid range.</exception>
    public ZstdHttpCompressor(int quality = DefaultQuality)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quality, MinQuality);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quality, MaxQuality);
        _quality = quality;
    }

    /// <inheritdoc />
    public override string ContentEncoding => "zstd";

    /// <inheritdoc />
    public override Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default)
        => Task.Run(() => Compress(data), cancellationToken);

    private byte[] Compress(byte[] data)
    {
        using var compressor = new Compressor(_quality);
        return compressor.Wrap(data).ToArray();
    }
}
