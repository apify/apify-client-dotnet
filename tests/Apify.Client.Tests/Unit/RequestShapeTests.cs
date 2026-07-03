using System;
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
}
