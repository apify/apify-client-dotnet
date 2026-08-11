using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Offline request-shape tests for mutating/convenience endpoints that are risky to exercise live on the
/// shared account. Each asserts the HTTP method, path, query and body the client actually sends.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RequestShapeTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task LastRunDatasetForwardsStatusAndOrigin()
    {
        var transport = new MockTransport().QueueResponse(200, "[]");
        await Client(transport).Actor("me/act")
            .LastRun(new LastRunOptions { Status = "SUCCEEDED", Origin = "API" })
            .Dataset()
            .ListItemsAsync(new DatasetListItemsOptions());

        var uri = transport.LastRequest.Uri;
        Assert.Contains("/actors/me~act/runs/last/dataset/items", uri, StringComparison.Ordinal);
        Assert.Contains("status=SUCCEEDED", uri, StringComparison.Ordinal);
        Assert.Contains("origin=API", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LastRunKeyValueStoreForwardsStatusAndOrigin()
    {
        var transport = new MockTransport().QueueResponse(200, "value");
        await Client(transport).Actor("me/act")
            .LastRun(new LastRunOptions { Status = "SUCCEEDED", Origin = "API" })
            .KeyValueStore()
            .GetRecordAsync("OUTPUT");

        var uri = transport.LastRequest.Uri;
        Assert.Contains("/actors/me~act/runs/last/key-value-store/records/OUTPUT", uri, StringComparison.Ordinal);
        Assert.Contains("status=SUCCEEDED", uri, StringComparison.Ordinal);
        Assert.Contains("origin=API", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LastRunRequestQueueForwardsStatusAndOrigin()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[]}}");
        await Client(transport).Actor("me/act")
            .LastRun(new LastRunOptions { Status = "SUCCEEDED", Origin = "API" })
            .RequestQueue()
            .ListHeadAsync();

        var uri = transport.LastRequest.Uri;
        Assert.Contains("/actors/me~act/runs/last/request-queue/head", uri, StringComparison.Ordinal);
        Assert.Contains("status=SUCCEEDED", uri, StringComparison.Ordinal);
        Assert.Contains("origin=API", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LastRunLogForwardsStatusAndOrigin()
    {
        var transport = new MockTransport().QueueResponse(200, "log output");
        await Client(transport).Actor("me/act")
            .LastRun(new LastRunOptions { Status = "SUCCEEDED", Origin = "API" })
            .Log()
            .GetAsync();

        var uri = transport.LastRequest.Uri;
        Assert.Contains("/actors/me~act/runs/last/log", uri, StringComparison.Ordinal);
        Assert.Contains("status=SUCCEEDED", uri, StringComparison.Ordinal);
        Assert.Contains("origin=API", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LastRunDatasetPushItemsForwardsStatusAndOrigin()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        await Client(transport).Actor("me/act")
            .LastRun(new LastRunOptions { Status = "SUCCEEDED", Origin = "API" })
            .Dataset()
            .PushItemsAsync(new { hello = "world" });

        var request = transport.LastRequest;
        Assert.Equal("POST", request.Method);
        Assert.Contains("/actors/me~act/runs/last/dataset/items", request.Uri, StringComparison.Ordinal);
        Assert.Contains("status=SUCCEEDED", request.Uri, StringComparison.Ordinal);
        Assert.Contains("origin=API", request.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunChargeSendsBodyAndIdempotencyKey()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        await Client(transport).Run("run1").ChargeAsync(new RunChargeOptions("result", count: 3));

        var request = transport.LastRequest;
        Assert.Equal("POST", request.Method);
        Assert.Contains("/actor-runs/run1/charge", request.Uri, StringComparison.Ordinal);
        Assert.NotEqual(string.Empty, request.Header("idempotency-key"));
        var body = JsonNode.Parse(request.Body)!;
        Assert.Equal("result", body["eventName"]!.GetValue<string>());
        Assert.Equal(3, body["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task RunChargeUsesProvidedIdempotencyKey()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        await Client(transport).Run("run1").ChargeAsync(new RunChargeOptions("e", idempotencyKey: "fixed-key"));
        Assert.Equal("fixed-key", transport.LastRequest.Header("idempotency-key"));
    }

    [Fact]
    public async Task MetamorphSendsTargetActorIdAndInput()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"r\"}}");
        await Client(transport).Run("run1").MetamorphAsync("apify/other", new { x = 1 }, new MetamorphOptions { Build = "latest" });

        var request = transport.LastRequest;
        Assert.Equal("POST", request.Method);
        Assert.Contains("/actor-runs/run1/metamorph", request.Uri, StringComparison.Ordinal);
        Assert.Contains("targetActorId=apify%2Fother", request.Uri, StringComparison.Ordinal);
        Assert.Contains("build=latest", request.Uri, StringComparison.Ordinal);
        Assert.Equal(1, JsonNode.Parse(request.Body)!["x"]!.GetValue<int>());
    }

    [Fact]
    public async Task ResurrectSendsOptions()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"r\"}}");
        await Client(transport).Run("run1").ResurrectAsync(new RunResurrectOptions { Build = "beta", MemoryMbytes = 1024 });

        var uri = transport.LastRequest.Uri;
        Assert.Contains("/actor-runs/run1/resurrect", uri, StringComparison.Ordinal);
        Assert.Contains("build=beta", uri, StringComparison.Ordinal);
        Assert.Contains("memory=1024", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RebootPostsToRebootPath()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"r\"}}");
        await Client(transport).Run("run1").RebootAsync();

        var request = transport.LastRequest;
        Assert.Equal("POST", request.Method);
        Assert.Contains("/actor-runs/run1/reboot", request.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AbortSendsGracefullyFlag()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"r\"}}");
        await Client(transport).Run("run1").AbortAsync(true);
        Assert.Contains("gracefully=1", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DefaultBuildFetchesBuildsDefault()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"build1\"}}");
        await Client(transport).Actor("me~a").DefaultBuildAsync(10);

        var request = transport.LastRequest;
        Assert.Equal("GET", request.Method);
        Assert.Contains("/actors/me~a/builds/default", request.Uri, StringComparison.Ordinal);
        Assert.Contains("waitForFinish=10", request.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestQueueOptionsApplyClientKey()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[]}}");
        await Client(transport).RequestQueue("q1", new RequestQueueClientOptions { ClientKey = "ck-123" }).ListHeadAsync(5);

        Assert.Contains("clientKey=ck-123", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestQueueOptionsApplyTimeout()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[]}}");
        await Client(transport).RequestQueue("q1", new RequestQueueClientOptions { TimeoutSecs = 2.0 }).ListHeadAsync(5);

        // The per-queue timeout must be threaded down to the transport (first attempt uses it directly).
        Assert.Equal(2.0, transport.Timeouts[0]);
    }

    [Fact]
    public async Task UpdateLimitsPutsToMeLimits()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        await Client(transport).Me().UpdateLimitsAsync(new { maxMonthlyUsageUsd = 100 });

        var request = transport.LastRequest;
        Assert.Equal("PUT", request.Method);
        Assert.Contains("/users/me/limits", request.Uri, StringComparison.Ordinal);
        Assert.Equal(100, JsonNode.Parse(request.Body)!["maxMonthlyUsageUsd"]!.GetValue<int>());
    }

    [Fact]
    public async Task PublishPutsIsPublicTrue()
    {
        var transport = new MockTransport().QueueResponse(
            200,
            "{\"data\":{\"id\":\"t1\",\"isPublic\":true,\"publicConfig\":{\"publishedAt\":\"2026-01-01T00:00:00.000Z\",\"seoTitle\":\"My task\"}}}");
        var task = await Client(transport).Task("t1").PublishAsync();

        var request = transport.LastRequest;
        Assert.Equal("PUT", request.Method);
        Assert.Contains("/actor-tasks/t1", request.Uri, StringComparison.Ordinal);
        Assert.True(JsonNode.Parse(request.Body)!["isPublic"]!.GetValue<bool>());
        Assert.True(task.IsPublic);
        Assert.Equal("2026-01-01T00:00:00.000Z", task.PublicConfig!.PublishedAt);
        Assert.Equal("My task", task.PublicConfig!.SeoTitle);
    }

    [Fact]
    public async Task UnpublishPutsIsPublicFalse()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"t1\",\"isPublic\":false}}");
        var task = await Client(transport).Task("t1").UnpublishAsync();

        var request = transport.LastRequest;
        Assert.Equal("PUT", request.Method);
        Assert.Contains("/actor-tasks/t1", request.Uri, StringComparison.Ordinal);
        Assert.False(JsonNode.Parse(request.Body)!["isPublic"]!.GetValue<bool>());
        Assert.False(task.IsPublic);
    }

    [Fact]
    public async Task DatasetListItemsJoinsMultiValueParamsAsCsv()
    {
        // fields/omit/unwind are list parameters: each is joined with a comma (URL-encoded as %2C).
        var transport = new MockTransport().QueueResponse(200, "[]");
        await Client(transport).Dataset("ds1").ListItemsAsync(new DatasetListItemsOptions
        {
            Fields = new[] { "name", "url" },
            Omit = new[] { "secret" },
            Unwind = new[] { "results" },
        });

        var uri = transport.LastRequest.Uri;
        Assert.Contains("fields=name%2Curl", uri, StringComparison.Ordinal);
        Assert.Contains("omit=secret", uri, StringComparison.Ordinal);
        Assert.Contains("unwind=results", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunListJoinsStatusAsCsv()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[],\"total\":0}}");
        await Client(transport).Runs().ListAsync(null, new RunListOptions { Status = new[] { "SUCCEEDED", "RUNNING" } });

        Assert.Contains("status=SUCCEEDED%2CRUNNING", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListRequestsJoinsFilterAsCsv()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[]}}");
        await Client(transport).RequestQueue("q1").ListRequestsAsync(new ListRequestsOptions
        {
            Filter = new[] { ListRequestsOptions.FilterLocked, ListRequestsOptions.FilterPending },
        });

        Assert.Contains("filter=locked%2Cpending", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MaxTotalChargeUsdIsFormattedWithInvariantCulture()
    {
        // Under a culture that uses a comma decimal separator, the double must still be sent with a '.'.
        var original = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"r\"}}");
            await Client(transport).Actor("act").StartAsync(null, new ActorStartOptions { MaxTotalChargeUsd = 12.5 });

            var uri = transport.LastRequest.Uri;
            Assert.Contains("maxTotalChargeUsd=12.5", uri, StringComparison.Ordinal);
            Assert.DoesNotContain("12%2C5", uri, StringComparison.Ordinal); // would appear if the comma culture leaked in
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public async Task LargeRequestBodyIsBrotliCompressed()
    {
        // A JSON body at or above the 1 KiB threshold is sent brotli-compressed: the transport sees the
        // "br" Content-Encoding, and brotli-decompressing the raw bytes recovers the original JSON.
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        var bigValue = new string('x', 4096);
        await Client(transport).Dataset("ds1").PushItemsAsync(new { blob = bigValue });

        var request = transport.LastRequest;
        Assert.Equal("br", request.Header("Content-Encoding"));

        using var input = new System.IO.MemoryStream(request.BodyBytes);
        using var brotli = new System.IO.Compression.BrotliStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new System.IO.MemoryStream();
        await brotli.CopyToAsync(output);
        var decoded = System.Text.Encoding.UTF8.GetString(output.ToArray());
        Assert.Equal(bigValue, JsonNode.Parse(decoded)!["blob"]!.GetValue<string>());
    }

    [Fact]
    public async Task LargeRequestBodyIsGzipCompressedWhenGzipSelected()
    {
        // With RequestCompression.Gzip selected, a body at or above the 1 KiB threshold is sent
        // gzip-compressed: the transport sees the "gzip" Content-Encoding, and gzip-decompressing the raw
        // bytes recovers the original JSON. This exercises the gzip code path end to end.
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        var client = new ApifyClient(new ApifyClientOptions
        {
            Token = "t",
            MinDelayBetweenRetriesMillis = 1,
            TimeoutSecs = 5,
            HttpTransport = transport,
            RequestCompression = RequestCompression.Gzip,
        });
        var bigValue = new string('x', 4096);
        await client.Dataset("ds1").PushItemsAsync(new { blob = bigValue });

        var request = transport.LastRequest;
        Assert.Equal("gzip", request.Header("Content-Encoding"));

        using var input = new System.IO.MemoryStream(request.BodyBytes);
        using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress);
        using var output = new System.IO.MemoryStream();
        await gzip.CopyToAsync(output);
        var decoded = System.Text.Encoding.UTF8.GetString(output.ToArray());
        Assert.Equal(bigValue, JsonNode.Parse(decoded)!["blob"]!.GetValue<string>());
    }

    [Fact]
    public async Task LargeRequestBodyUsesBrotliByDefault()
    {
        // The default RequestCompression is brotli, so a large body is brotli-compressed even without any
        // explicit option, keeping the brotli path the reference-preferred default.
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        await Client(transport).Dataset("ds1").PushItemsAsync(new { blob = new string('y', 4096) });

        Assert.Equal("br", transport.LastRequest.Header("Content-Encoding"));
    }

    [Theory]
    // The threshold (MinCompressBytes = 1024) is inclusive: exactly 1024 bytes is compressed, 1023 is not.
    // A raw byte payload gives exact control over the body size (no JSON framing to account for).
    [InlineData(1024, "br")]
    [InlineData(1023, "")]
    public async Task CompressionThresholdIsInclusiveAt1024Bytes(int size, string expectedEncoding)
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        var payload = new byte[size]; // zero-filled: size is exact and it compresses when over the threshold
        await Client(transport).KeyValueStore("s1").SetRecordAsync("OUTPUT", payload, "application/octet-stream");

        Assert.Equal(expectedEncoding, transport.LastRequest.Header("Content-Encoding"));
    }

    [Fact]
    public async Task SmallRequestBodyIsNotCompressed()
    {
        // A body below the 1 KiB threshold is sent verbatim with no Content-Encoding header.
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        await Client(transport).Dataset("ds1").PushItemsAsync(new { blob = "small" });

        var request = transport.LastRequest;
        Assert.Equal(string.Empty, request.Header("Content-Encoding"));
        Assert.Equal("small", JsonNode.Parse(request.Body)!["blob"]!.GetValue<string>());
    }

    [Fact]
    public async Task SetRecordSendsRawBytesWithVerbatimContentType()
    {
        // A binary write must send the bytes verbatim (incl. 0xFF) as ByteArrayContent and set the
        // content type exactly as given, without appending "; charset=...".
        var bytes = new byte[] { 0x00, 0xFF, 0x10, 0x7F };
        var transport = new MockTransport().QueueResponse(200, string.Empty);
        await Client(transport).KeyValueStore("s1").SetRecordAsync("OUTPUT", bytes, "application/octet-stream");

        var request = transport.LastRequest;
        Assert.Equal("PUT", request.Method);
        Assert.Contains("/key-value-stores/s1/records/OUTPUT", request.Uri, StringComparison.Ordinal);
        Assert.Equal("application/octet-stream", request.Header("Content-Type"));
        Assert.DoesNotContain("charset", request.Header("Content-Type"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(bytes, request.BodyBytes);
    }
}
