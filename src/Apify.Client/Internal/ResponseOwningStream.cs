using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Apify.Client.Internal;

/// <summary>
/// A read-only stream over an HTTP response body that also owns the <see cref="HttpResponseMessage"/>,
/// disposing it (and thus releasing the connection) when the stream is disposed. Used for live log
/// streaming, where the response must stay open while the caller reads the body incrementally.
/// </summary>
internal sealed class ResponseOwningStream : Stream
{
    private readonly HttpResponseMessage _response;
    private readonly Stream _inner;

    private ResponseOwningStream(HttpResponseMessage response, Stream inner)
    {
        _response = response;
        _inner = inner;
    }

    /// <summary>Wraps a response's body stream, transferring ownership of the response to the returned stream.</summary>
    public static async Task<Stream> CreateAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var inner = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new ResponseOwningStream(response, inner);
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override void Flush() => _inner.Flush();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
        }

        base.Dispose(disposing);
    }
}
