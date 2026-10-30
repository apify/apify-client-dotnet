using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Http;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Client-level behaviour that mirrors the reference client: request statistics, the separate long and
/// maximum timeouts, the public base URL default, the User-Agent override and non-retried dataset pushes.
/// </summary>
[Trait("Category", "Unit")]
[Collection("Console")]
public sealed class ClientOptionsAndStatsTests
{
    private const string NotFoundBody = "{\"error\":{\"type\":\"record-not-found\",\"message\":\"missing\"}}";

    private static ApifyClient Client(MockTransport transport, Action<ApifyClientOptions>? configure = null)
    {
        var options = new ApifyClientOptions
        {
            Token = "t",
            MinDelayBetweenRetriesMillis = 1,
            TimeoutSecs = 100,
            HttpTransport = transport,
        };
        configure?.Invoke(options);
        return new ApifyClient(options);
    }

    [Fact]
    public async Task PushItemsDoesNotRetryATimedOutRequest()
    {
        var transport = new MockTransport().QueueError(timeout: true).QueueResponse(201, string.Empty);

        var error = await Assert.ThrowsAsync<ApifyTransportException>(
            () => Client(transport).Dataset("ds1").PushItemsAsync(new { a = 1 }));

        Assert.True(error.IsTimeout);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task PushItemsStillRetriesANonTimeoutNetworkError()
    {
        var transport = new MockTransport().QueueError().QueueResponse(201, string.Empty);

        await Client(transport).Dataset("ds1").PushItemsAsync(new { a = 1 });

        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task PublicBaseUrlDefaultsToThePublicApiWhateverTheBaseUrlIs()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        var client = Client(transport, o => o.BaseUrl = "http://internal.local:8080");

        var url = await client.KeyValueStore("s1").GetRecordPublicUrlAsync("k");

        Assert.StartsWith("https://api.apify.com/v2/key-value-stores/s1/records/k", url, StringComparison.Ordinal);
        Assert.StartsWith("http://internal.local:8080/v2/", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExplicitPublicBaseUrlIsUsed()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        var client = Client(transport, o => o.PublicBaseUrl = "https://public.example");

        var url = await client.KeyValueStore("s1").GetRecordPublicUrlAsync("k");

        Assert.StartsWith("https://public.example/v2/key-value-stores/s1/records/k", url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StatsCountCallsRequestsAndRateLimitErrorsPerAttempt()
    {
        var transport = new MockTransport().QueueResponse(429, "{\"error\":{\"type\":\"x\",\"message\":\"slow down\"}}").QueueResponse(200, SpecFixtures.Actor("x"));
        var client = Client(transport);

        await client.Actor("x").GetAsync();

        Assert.Equal(1, client.Stats.Calls);
        Assert.Equal(2, client.Stats.Requests);
        Assert.Equal(new long[] { 1 }, client.Stats.RateLimitErrors);
    }

    [Fact]
    public async Task StatsRateLimitErrorsAreIndexedByAttempt()
    {
        var transport = new MockTransport()
            .QueueResponse(200, SpecFixtures.Actor("x"))
            .QueueResponse(429, "{}").QueueResponse(429, "{}").QueueResponse(200, SpecFixtures.Actor("x"));
        var client = Client(transport);

        await client.Actor("x").GetAsync();
        await client.Actor("x").GetAsync();

        Assert.Equal(2, client.Stats.Calls);
        Assert.Equal(4, client.Stats.Requests);
        Assert.Equal(new long[] { 1, 1 }, client.Stats.RateLimitErrors);
    }

    [Fact]
    public async Task LongTierUsesTimeoutLongSecsWhileOverridesAreCappedAtTimeoutSecs()
    {
        var transport = new MockTransport()
            .QueueResponse(404, NotFoundBody)
            .QueueResponse(404, NotFoundBody);
        var client = Client(transport, o =>
        {
            o.TimeoutSecs = 100;
            o.TimeoutLongSecs = 20;
        });

        // HEAD on a record is a Long-tier call.
        await client.KeyValueStore("s1").RecordExistsAsync("k");
        await client.KeyValueStore("s1").WithTimeout(TimeSpan.FromSeconds(500)).RecordExistsAsync("k");

        Assert.Equal(20.0, transport.Timeouts[0]);
        Assert.Equal(100.0, transport.Timeouts[1]);
    }

    [Fact]
    public async Task LongTierDefaultsToTimeoutSecs()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);

        await Client(transport).KeyValueStore("s1").RecordExistsAsync("k");

        Assert.Equal(100.0, transport.Timeouts[0]);
    }

    [Fact]
    public async Task UserAgentPropertyReflectsAHeadersOverride()
    {
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.Actor("x"));
        var client = Client(transport, o => o.Headers = new Dictionary<string, string> { ["user-agent"] = "custom-agent" });

        await client.Actor("x").GetAsync();

        Assert.Equal("custom-agent", client.UserAgent);
        Assert.Equal(client.UserAgent, transport.LastRequest.Header("User-Agent"));
    }

    [Fact]
    public void UserAgentPropertyIsTheBuiltInValueByDefault()
    {
        var client = Client(new MockTransport());

        Assert.Contains("ApifyClient/", client.UserAgent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GzipCompressorRejectsAnUndefinedLevel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GzipHttpCompressor((CompressionLevel)42));
    }

    [Fact]
    public void PrefixedSinkWritesTheReferencePrefix()
    {
        var writer = new StringWriter { NewLine = "\n" };

        var sink = StreamedLog.CreatePrefixedSink("run1", "my-actor", writer);
        sink("hello");
        StreamedLog.CreatePrefixedSink("run2", null, writer)("bye");

        Assert.Equal("my-actor runId:run1 -> hello\nrunId:run2 -> bye\n", writer.ToString());
    }

    [Fact]
    public async Task InvalidKeyAndRequestIdErrorsNameTheCallersParameter()
    {
        var transport = new MockTransport();
        var client = Client(transport);

        var key = await Assert.ThrowsAsync<ArgumentException>(() => client.KeyValueStore("s1").GetRecordAsync(string.Empty));
        var id = await Assert.ThrowsAsync<ArgumentException>(() => client.RequestQueue("q1").DeleteRequestAsync(".."));

        var log = Assert.Throws<ArgumentException>(() => client.Log(".."));
        var envVar = Assert.Throws<ArgumentException>(() => client.Actor("a").Version("0.1").EnvVar(".."));

        Assert.Equal("key", key.ParamName);
        Assert.Equal("id", id.ParamName);
        Assert.Equal("buildOrRunId", log.ParamName);
        Assert.Equal("name", envVar.ParamName);
        Assert.Equal(0, transport.CallCount);
    }

    /// <summary>Answers by URL so the concurrent log stream and the run polling cannot race on a queue.</summary>
    private sealed class RoutingTransport : IHttpTransport
    {
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, TimeSpan timeout, bool streaming, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsolutePath;
            var body = url switch
            {
                _ when url.EndsWith("/log", StringComparison.Ordinal) => "2024-01-02T03:04:05.678Z hello from the run\n",
                _ when url.EndsWith("/runs", StringComparison.Ordinal) => SpecFixtures.Run("run1", "RUNNING"),
                _ when url.Contains("/actor-runs/", StringComparison.Ordinal) => SpecFixtures.Run("run1"),
                _ => SpecFixtures.Actor("a", "my-actor"),
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }
    }

    [Fact]
    public async Task CallAsyncWithTheDefaultConsoleSinkRedirectsTheLogWithTheReferencePrefix()
    {
        var client = new ApifyClient(new ApifyClientOptions { Token = "t", HttpTransport = new RoutingTransport() });
        var original = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            await client.Actor("a").CallAsync(log: StreamedLog.DefaultConsole);
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Equal("my-actor runId:run1 -> 2024-01-02T03:04:05.678Z hello from the run\n", writer.ToString());
    }
}
