using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Apify.Client.Http;

/// <summary>
/// Compresses request bodies with gzip (<c>Content-Encoding: gzip</c>). Matches the reference client's
/// <c>GzipHttpCompressor</c>.
/// </summary>
public sealed class GzipHttpCompressor : HttpCompressor
{
    private readonly CompressionLevel _level;

    /// <summary>Creates a gzip compressor.</summary>
    /// <param name="level">
    /// The compression level. .NET exposes zlib's levels as three named values: <see cref="CompressionLevel.Fastest"/>
    /// is level 1, <see cref="CompressionLevel.Optimal"/> level 6 (the default, as in the reference client) and
    /// <see cref="CompressionLevel.SmallestSize"/> level 9.
    /// </param>
    public GzipHttpCompressor(CompressionLevel level = CompressionLevel.Optimal)
    {
        _level = level;
    }

    /// <inheritdoc />
    public override string ContentEncoding => "gzip";

    /// <inheritdoc />
    public override Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default)
        => Task.Run(() => Compress(data), cancellationToken);

    private byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, _level))
        {
            gzip.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }
}
