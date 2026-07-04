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
