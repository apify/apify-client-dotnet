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

        // Surface any exception thrown while the server wrote the response, so a server-side failure fails
        // the test explicitly instead of hiding behind a client-side timeout.
        await server.WaitForResponseWrittenAsync();
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
        /// <summary>How long the test waits for the server to finish writing before giving up.</summary>
        private static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(30);

        /// <summary>Attempts to claim a free port before failing (guards the reserve-then-bind race).</summary>
        private const int BindAttempts = 20;

        private readonly HttpListener _listener = new();
        private readonly string _encoding;
        private readonly byte[] _payload;
        private Task? _serveTask;

        public LoopbackServer(string encoding, byte[] payload)
        {
            _encoding = encoding;
            _payload = payload;
            BaseUrl = ClaimListenerPort(_listener);
        }

        public string BaseUrl { get; }

        public void Start()
        {
            _serveTask = Task.Run(async () =>
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                var response = context.Response;
                try
                {
                    response.StatusCode = 200;
                    response.ContentType = "application/json";
                    response.AddHeader("Content-Encoding", _encoding);
                    await response.OutputStream.WriteAsync(_payload).ConfigureAwait(false);
                }
                finally
                {
                    response.OutputStream.Close();
                }
            });
        }

        /// <summary>Awaits the single response having been written, propagating any server-side exception.</summary>
        public async Task WaitForResponseWrittenAsync()
        {
            if (_serveTask is null)
            {
                return;
            }

            var finished = await Task.WhenAny(_serveTask, Task.Delay(WriteTimeout)).ConfigureAwait(false);
            Assert.True(finished == _serveTask, "loopback server did not finish writing the response in time");
            await _serveTask.ConfigureAwait(false); // rethrows any server-side failure
        }

        public void Dispose() => _listener.Close();

        /// <summary>
        /// Binds and starts the listener on a free loopback port, returning its base URL. A port is reserved
        /// by briefly binding a <see cref="TcpListener"/> to port 0 and reusing the number; because that
        /// leaves a race where the port could be re-taken before <see cref="HttpListener"/> claims it,
        /// binding is retried on conflict rather than assumed to succeed the first time. The listener is left
        /// running so <see cref="Start"/> can begin accepting immediately.
        /// </summary>
        private static string ClaimListenerPort(HttpListener listener)
        {
            for (var attempt = 1; ; attempt++)
            {
                var baseUrl = $"http://127.0.0.1:{ReserveFreePort()}";
                listener.Prefixes.Clear();
                listener.Prefixes.Add(baseUrl + "/");
                try
                {
                    listener.Start();
                    return baseUrl;
                }
                catch (HttpListenerException) when (attempt < BindAttempts)
                {
                    // The reserved port was taken between reservation and binding; try another.
                }
            }
        }

        /// <summary>Reserves a free TCP port by briefly binding to port 0, then releasing it.</summary>
        private static int ReserveFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }
}
