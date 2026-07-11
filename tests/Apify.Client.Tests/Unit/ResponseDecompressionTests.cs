using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Apify.Client;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Verifies that the default <see cref="Apify.Client.Http.HttpClientTransport"/> transparently decompresses
/// brotli/gzip/deflate responses, matching the API's documented response compression. A real loopback HTTP
/// server is used because automatic decompression is a property of the default handler, not of the scripted
/// <see cref="MockTransport"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResponseDecompressionTests
{
    [Theory]
    [InlineData("gzip")]
    [InlineData("br")]
    [InlineData("deflate")]
    public async Task DefaultTransportDecompressesResponse(string encoding)
    {
        const string json = "{\"data\":{\"id\":\"act1\",\"name\":\"compressed-actor\"}}";
        var payload = Compress(Encoding.UTF8.GetBytes(json), encoding);

        using var server = new LoopbackServer(encoding, payload);
        server.Start();

        var client = new ApifyClient(new ApifyClientOptions
        {
            Token = "test-token",
            BaseUrl = server.BaseUrl,
        });

        var actor = await client.Actor("act1").GetAsync();

        Assert.NotNull(actor);
        Assert.Equal("act1", actor!.Id);
        Assert.Equal("compressed-actor", actor.Name);
    }

    private static byte[] Compress(byte[] data, string encoding)
    {
        using var output = new MemoryStream();
        using (Stream compressor = encoding switch
        {
            "gzip" => new GZipStream(output, CompressionMode.Compress),
            "br" => new BrotliStream(output, CompressionMode.Compress),
            "deflate" => new DeflateStream(output, CompressionMode.Compress),
            _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "unsupported encoding"),
        })
        {
            compressor.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    /// <summary>A minimal single-response loopback HTTP server returning a compressed body.</summary>
    private sealed class LoopbackServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly string _encoding;
        private readonly byte[] _payload;

        public LoopbackServer(string encoding, byte[] payload)
        {
            _encoding = encoding;
            _payload = payload;
            var port = FreePort();
            BaseUrl = $"http://127.0.0.1:{port}";
            _listener.Prefixes.Add(BaseUrl + "/");
        }

        public string BaseUrl { get; }

        public void Start()
        {
            _listener.Start();
            _ = Task.Run(async () =>
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                var response = context.Response;
                response.StatusCode = 200;
                response.ContentType = "application/json";
                response.AddHeader("Content-Encoding", _encoding);
                response.OutputStream.Write(_payload, 0, _payload.Length);
                response.OutputStream.Close();
            });
        }

        public void Dispose() => _listener.Close();

        /// <summary>Reserves a free TCP port by briefly binding to port 0, then releasing it.</summary>
        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
