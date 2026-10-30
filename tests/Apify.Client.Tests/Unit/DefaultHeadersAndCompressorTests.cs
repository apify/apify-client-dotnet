using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Http;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// The client-wide default headers and the pluggable request body compressor, matching the reference
/// client's <c>headers</c> and <c>compression</c> options.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DefaultHeadersAndCompressorTests
{
    private static readonly string LargeItem = "{\"blob\":\"" + new string('x', 4096) + "\"}";

    private static ApifyClient Client(MockTransport transport, Action<ApifyClientOptions>? configure = null)
    {
        var options = new ApifyClientOptions
        {
            Token = "t",
            MinDelayBetweenRetriesMillis = 1,
            TimeoutSecs = 5,
            HttpTransport = transport,
        };
        configure?.Invoke(options);
        return new ApifyClient(options);
    }

    private static async Task<string> Decompress(byte[] data, Func<Stream, Stream> wrap)
    {
        using var input = new MemoryStream(data);
        await using var decompressor = wrap(input);
        using var output = new MemoryStream();
        await decompressor.CopyToAsync(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    [Fact]
    public async Task DefaultHeadersAreSentWithEveryRequest()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty).QueueResponse(200, string.Empty);
        var client = Client(transport, o => o.Headers = new Dictionary<string, string> { ["X-Integration"] = "my-platform" });

        await client.Dataset("ds1").PushItemsAsync(new { a = 1 });
        await client.Dataset("ds1").PushItemsAsync(new { a = 2 });

        Assert.Equal("my-platform", transport.Received[0].Header("X-Integration"));
        Assert.Equal("my-platform", transport.Received[1].Header("X-Integration"));
    }

    [Fact]
    public async Task DefaultHeadersWinOverTheBuiltInDefaultsCaseInsensitively()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        var client = Client(transport, o => o.Headers = new Dictionary<string, string>
        {
            ["user-agent"] = "custom-agent",
            ["authorization"] = "Bearer other",
        });

        await client.Dataset("ds1").PushItemsAsync(new { a = 1 });

        Assert.Equal("custom-agent", transport.LastRequest.Header("User-Agent"));
        Assert.Equal("Bearer other", transport.LastRequest.Header("Authorization"));
    }

    [Fact]
    public async Task BuiltInDefaultsRemainWhenNoDefaultHeadersAreGiven()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);

        await Client(transport).Dataset("ds1").PushItemsAsync(new { a = 1 });

        Assert.Equal("Bearer t", transport.LastRequest.Header("Authorization"));
        Assert.StartsWith("ApifyClient/", transport.LastRequest.Header("User-Agent"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACustomCompressorIsUsedForLargeBodies()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        var client = Client(transport, o => o.Compressor = new MarkerCompressor());

        await client.Dataset("ds1").PushItemsAsync(JsonNodeOf(LargeItem));

        Assert.Equal("marker", transport.LastRequest.Header("Content-Encoding"));
        Assert.Equal(Encoding.UTF8.GetBytes("MARK:" + LargeItem), transport.LastRequest.BodyBytes);
    }

    [Fact]
    public async Task ACustomCompressorIsNotUsedForSmallBodies()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        var client = Client(transport, o => o.Compressor = new MarkerCompressor());

        await client.Dataset("ds1").PushItemsAsync(new { a = 1 });

        Assert.Equal(string.Empty, transport.LastRequest.Header("Content-Encoding"));
    }

    [Fact]
    public async Task ACustomCompressorTakesPrecedenceOverTheRequestCompressionOption()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        var client = Client(transport, o =>
        {
            o.RequestCompression = RequestCompression.Gzip;
            o.Compressor = new BrotliHttpCompressor(quality: 1);
        });

        await client.Dataset("ds1").PushItemsAsync(JsonNodeOf(LargeItem));

        Assert.Equal("br", transport.LastRequest.Header("Content-Encoding"));
    }

    [Theory]
    [InlineData(BrotliHttpCompressor.MinQuality)]
    [InlineData(BrotliHttpCompressor.DefaultQuality)]
    [InlineData(BrotliHttpCompressor.MaxQuality)]
    public async Task BrotliCompressorRoundTripsAtEveryQuality(int quality)
    {
        var compressor = new BrotliHttpCompressor(quality);

        var compressed = await compressor.CompressAsync(Encoding.UTF8.GetBytes(LargeItem));

        Assert.Equal("br", compressor.ContentEncoding);
        Assert.Equal(LargeItem, await Decompress(compressed, s => new BrotliStream(s, CompressionMode.Decompress)));
    }

    [Theory]
    [InlineData(BrotliHttpCompressor.MinQuality - 1)]
    [InlineData(BrotliHttpCompressor.MaxQuality + 1)]
    public void BrotliCompressorRejectsAnOutOfRangeQuality(int quality)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BrotliHttpCompressor(quality));
    }

    [Theory]
    [InlineData(CompressionLevel.Fastest)]
    [InlineData(CompressionLevel.Optimal)]
    [InlineData(CompressionLevel.SmallestSize)]
    public async Task GzipCompressorRoundTripsAtEveryLevel(CompressionLevel level)
    {
        var compressor = new GzipHttpCompressor(level);

        var compressed = await compressor.CompressAsync(Encoding.UTF8.GetBytes(LargeItem));

        Assert.Equal("gzip", compressor.ContentEncoding);
        Assert.Equal(LargeItem, await Decompress(compressed, s => new GZipStream(s, CompressionMode.Decompress)));
    }

    private static System.Text.Json.Nodes.JsonNode JsonNodeOf(string json) => System.Text.Json.Nodes.JsonNode.Parse(json)!;

    /// <summary>A stand-in compressor that prefixes the body, so a test can recognize it.</summary>
    private sealed class MarkerCompressor : HttpCompressor
    {
        public override string ContentEncoding => "marker";

        public override Task<byte[]> CompressAsync(byte[] data, CancellationToken cancellationToken = default)
            => Task.FromResult(Encoding.UTF8.GetBytes("MARK:" + Encoding.UTF8.GetString(data)));
    }

    [Theory]
    [InlineData("Content-Type")]
    [InlineData("content-encoding")]
    [InlineData("Content-Length")]
    public void ContentHeadersAreRejectedAsDefaultHeaders(string name)
    {
        var transport = new MockTransport();

        var error = Assert.Throws<ArgumentException>(
            () => Client(transport, o => o.Headers = new Dictionary<string, string> { [name] = "x" }));

        Assert.Contains(name, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GzipCompressorDefaultsToTheOptimalLevel()
    {
        var data = Encoding.UTF8.GetBytes(LargeItem);

        var byDefault = await new GzipHttpCompressor().CompressAsync(data);
        var optimal = await new GzipHttpCompressor(CompressionLevel.Optimal).CompressAsync(data);

        Assert.Equal(optimal, byDefault);
    }
}
