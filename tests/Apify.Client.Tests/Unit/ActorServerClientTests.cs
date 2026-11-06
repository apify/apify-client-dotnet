using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Http;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// <see cref="Apify.Client.Resources.ActorServerClient"/>: requests to a Server Actor's web server, matching the
/// reference client's <c>ActorServerClient</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ActorServerClientTests
{
    private const string ServerUrl = "https://jane--echo.apify.actor";
    private static readonly string ActorResponse = SpecFixtures.Actor("a1", "echo", extraProperties: "\"standbyUrl\":\"" + ServerUrl + "\"");
    private static readonly string LargeBody = new('x', 4096);

    private static ApifyClient Client(MockTransport transport, int maxRetries = 2) => new(new ApifyClientOptions
    {
        Token = "secret-token",
        MaxRetries = maxRetries,
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task RequestSendsTheTokenInTheServerHeaderOnlyAndResolvesThePathAgainstTheServerUrl()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(200, "{\"ok\":true}");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync(
            "/search",
            new ActorServerRequestOptions
            {
                Method = HttpMethod.Post,
                Query = new Dictionary<string, object?> { ["q"] = "apify", ["limit"] = 5, ["exact"] = true, ["skipped"] = null },
                Body = new { limit = 5 },
            });

        Assert.Equal(200, response.StatusCode);
        Assert.True((await response.ReadAsJsonAsync())!["ok"]!.GetValue<bool>());
        Assert.Contains("/v2/actors/a1", transport.Received[0].Uri, StringComparison.Ordinal);

        var request = transport.Received[1];
        Assert.Equal("POST", request.Method);
        Assert.Equal(ServerUrl + "/search?q=apify&limit=5&exact=1", request.Uri);
        Assert.Equal("Bearer secret-token", request.Header("X-Apify-Authorization"));
        Assert.Equal(string.Empty, request.Header("Authorization"));
        Assert.StartsWith("ApifyClient/", request.Header("User-Agent"), StringComparison.Ordinal);
        Assert.Equal("application/json", request.Header("Content-Type"));
        Assert.Equal("{\"limit\":5}", request.Body);
    }

    [Fact]
    public async Task RequestDoesNotFollowRedirectsButApiCallsDo()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(200, "{}");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync();

        Assert.True(transport.Received[0].FollowsRedirects);
        Assert.False(transport.Received[1].FollowsRedirects);
        Assert.Equal(ServerUrl + "/", transport.Received[1].Uri);
    }

    [Fact]
    public async Task RequestKeepsTheCallerHeadersAndLetsThemReplaceTheTokenHeader()
    {
        var transport = new MockTransport()
            .QueueResponse(200, ActorResponse).QueueResponse(200, "{}").QueueResponse(200, "{}");
        var server = Client(transport).Actor("a1").Server();

        using var withOwnAuth = await server.RequestAsync(
            "/echo",
            new ActorServerRequestOptions { Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer actor-token" } });
        using var withOwnToken = await server.RequestAsync(
            "/echo",
            new ActorServerRequestOptions { Headers = new Dictionary<string, string> { ["X-Apify-Authorization"] = "Bearer other" } });

        Assert.Equal("Bearer actor-token", transport.Received[1].Header("Authorization"));
        Assert.Equal("Bearer secret-token", transport.Received[1].Header("X-Apify-Authorization"));
        Assert.Equal("Bearer other", transport.Received[2].Header("X-Apify-Authorization"));
    }

    [Fact]
    public async Task RequestDropsTheFragmentAndKeepsTheRequestOnTheServerHost()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(200, "{}").QueueResponse(200, "{}");
        var server = Client(transport).Actor("a1").Server();

        using var withFragment = await server.RequestAsync("/page#section");
        using var withHost = await server.RequestAsync("//evil.example/steal");

        Assert.Equal(ServerUrl + "/page", transport.Received[1].Uri);
        Assert.Equal(ServerUrl + "/evil.example/steal", transport.Received[2].Uri);
    }

    [Fact]
    public async Task RequestReturnsErrorStatusesWithoutThrowingOrRetrying()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(404, "{\"detail\":\"nope\"}");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync("/missing");

        Assert.Equal(404, response.StatusCode);
        Assert.False(response.IsSuccess);
        Assert.Contains("nope", await response.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task RequestRetriesRateLimitAndServiceUnavailableAndReturnsTheLastOneWhenRetriesRunOut()
    {
        var transport = new MockTransport()
            .QueueResponse(200, ActorResponse)
            .QueueResponse(429, "busy")
            .QueueResponse(503, "unavailable")
            .QueueResponse(503, "still unavailable");
        var client = Client(transport);

        using var response = await client.Actor("a1").Server().RequestAsync("/slow");

        Assert.Equal(503, response.StatusCode);
        Assert.Equal("still unavailable", await response.ReadAsStringAsync());
        // The actor lookup and three attempts: the first try and two retries.
        Assert.Equal(4, transport.CallCount);
        // The statistics track the API's rate limits, and this is not an API call.
        Assert.Empty(client.Stats.RateLimitErrors);
    }

    [Fact]
    public async Task RequestSucceedsAfterARetriedServiceUnavailable()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(503, "cold").QueueResponse(200, "warm");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync("/");

        Assert.Equal("warm", await response.ReadAsStringAsync());
    }

    [Fact]
    public async Task RequestDoesNotRetryOtherServerErrorStatuses()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(500, "boom");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync("/");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task RequestDoesNotRetryTransportErrorsBecauseTheActorMayHaveHandledTheRequest()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueError().QueueResponse(200, "{}");

        await Assert.ThrowsAsync<ApifyTransportException>(() => Client(transport).Actor("a1").Server().RequestAsync("/"));

        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task RequestSendsBodiesUncompressedUnlessACompressorIsSet()
    {
        var transport = new MockTransport()
            .QueueResponse(200, ActorResponse).QueueResponse(200, "{}").QueueResponse(200, "{}");
        var server = Client(transport).Actor("a1").Server();

        using var plain = await server.RequestAsync("/", new ActorServerRequestOptions { Method = HttpMethod.Post, Body = new { blob = LargeBody } });
        using var gzipped = await server.RequestAsync(
            "/",
            new ActorServerRequestOptions { Method = HttpMethod.Post, Body = new { blob = LargeBody }, Compressor = new GzipHttpCompressor() });

        Assert.Equal(string.Empty, transport.Received[1].Header("Content-Encoding"));
        Assert.Equal("gzip", transport.Received[2].Header("Content-Encoding"));
        Assert.Contains(LargeBody, transport.Received[2].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestSendsStringsAndBytesAsTheyAreWithTheirContentTypes()
    {
        var transport = new MockTransport()
            .QueueResponse(200, ActorResponse).QueueResponse(200, "{}").QueueResponse(200, "{}").QueueResponse(200, "{}");
        var server = Client(transport).Actor("a1").Server();

        using var text = await server.RequestAsync("/", new ActorServerRequestOptions { Method = HttpMethod.Put, Body = "plain \"text\"" });
        using var bytes = await server.RequestAsync("/", new ActorServerRequestOptions { Method = HttpMethod.Put, Body = new byte[] { 1, 2, 3 } });
        using var custom = await server.RequestAsync(
            "/",
            new ActorServerRequestOptions { Method = HttpMethod.Put, Body = "a=1", ContentType = "application/x-www-form-urlencoded" });

        Assert.Equal("plain \"text\"", transport.Received[1].Body);
        Assert.StartsWith("text/plain", transport.Received[1].Header("Content-Type"), StringComparison.Ordinal);
        Assert.Equal(new byte[] { 1, 2, 3 }, transport.Received[2].BodyBytes);
        Assert.Equal("application/octet-stream", transport.Received[2].Header("Content-Type"));
        Assert.Equal("application/x-www-form-urlencoded", transport.Received[3].Header("Content-Type"));
    }

    [Fact]
    public async Task RequestForwardsAPreEncodedBodyAsItIsWithItsContentEncoding()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(200, "{}");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync(
            "/",
            new ActorServerRequestOptions
            {
                Method = HttpMethod.Post,
                Body = new byte[] { 9, 8, 7 },
                Headers = new Dictionary<string, string> { ["Content-Encoding"] = "identity" },
                Compressor = new GzipHttpCompressor(),
            });

        Assert.Equal(new byte[] { 9, 8, 7 }, transport.Received[1].BodyBytes);
        Assert.Equal("identity", transport.Received[1].Header("Content-Encoding"));
    }

    [Fact]
    public async Task RequestUsesTheLongTimeoutTierUnlessOneIsGiven()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(200, "{}").QueueResponse(200, "{}");
        var server = Client(transport).Actor("a1").Server();

        using var byDefault = await server.RequestAsync("/");
        using var explicitTimeout = await server.RequestAsync("/", new ActorServerRequestOptions { Timeout = TimeSpan.FromSeconds(2) });

        Assert.Equal(5, transport.Timeouts[1]);
        Assert.Equal(2, transport.Timeouts[2]);
    }

    [Fact]
    public async Task RequestExposesHeadersAndRaisesInvalidResponseBodyForUnparsableJson()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse)
            .QueueResponse(200, "<html>", new Dictionary<string, string> { ["X-Custom"] = "yes" });

        using var response = await Client(transport).Actor("a1").Server().RequestAsync("/");

        Assert.Equal("yes", response.Headers["x-custom"]);
        Assert.Equal("<html>", await response.ReadAsStringAsync());
        await Assert.ThrowsAsync<InvalidResponseBodyException>(() => response.ReadAsJsonAsync());
    }

    [Fact]
    public async Task RequestThrowsForAnActorThatIsNotAServerActor()
    {
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.Actor("a1", extraProperties: "\"standbyUrl\":null"));

        var ex = await Assert.ThrowsAsync<ActorServerException>(() => Client(transport).Actor("a1").Server().RequestAsync("/"));

        Assert.Contains("not a Server Actor", ex.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<ApifyClientException>(ex);
    }

    [Fact]
    public async Task GetUrlFetchesTheActorOnceAndReusesTheUrl()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse);
        var server = Client(transport).Actor("a1").Server();

        Assert.Equal(ServerUrl, await server.GetUrlAsync());
        Assert.Equal(ServerUrl, await server.GetUrlAsync());

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task GetUrlDoesNotKeepAFailedLookup()
    {
        var transport = new MockTransport()
            .QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"no actor\"}}")
            .QueueResponse(200, ActorResponse);
        var server = Client(transport).Actor("a1").Server();

        await Assert.ThrowsAsync<NotFoundException>(() => server.GetUrlAsync());

        Assert.Equal(ServerUrl, await server.GetUrlAsync());
    }

    [Fact]
    public void ServerReturnsTheSameClientForTheSameActorClient()
    {
        var actor = Client(new MockTransport()).Actor("a1");

        Assert.Same(actor.Server(), actor.Server());
    }

    private static string ServerActor(string? extraProperties = null)
        => SpecFixtures.Actor("a1", extraProperties: "\"standbyUrl\":\"" + ServerUrl + "\"" + (extraProperties is null ? string.Empty : "," + extraProperties));

    [Fact]
    public async Task GetOpenApiSpecReadsTheWebServerSchemaOfTheLatestBuildByDefault()
    {
        var actor = ServerActor("\"taggedBuilds\":{\"latest\":{\"buildId\":\"b1\",\"buildNumber\":\"0.0.3\"}}");
        var build = SpecFixtures.Build("b1", extraProperties: "\"actorDefinition\":{\"webServerSchema\":{\"openapi\":\"3.0.0\"}}");
        var transport = new MockTransport().QueueResponse(200, actor).QueueResponse(200, build);

        var spec = await Client(transport).Actor("a1").Server().GetOpenApiSpecAsync();

        Assert.Equal("3.0.0", spec!["openapi"]!.GetValue<string>());
        Assert.Contains("/v2/actor-builds/b1", transport.Received[1].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOpenApiSpecResolvesAConfiguredBuildNumberThroughTheBuildList()
    {
        var actor = ServerActor("\"actorStandby\":{\"build\":\"0.0.2\"},\"taggedBuilds\":{}");
        var builds = SpecFixtures.Page(3, SpecFixtures.BuildItem("b3", "0.0.3"), SpecFixtures.BuildItem("b2", "0.0.2"), SpecFixtures.BuildItem("b1", "0.0.1"));
        var build = SpecFixtures.Build("b2", extraProperties: "\"actorDefinition\":{\"webServerSchema\":{\"openapi\":\"3.1.0\"}}");
        var transport = new MockTransport().QueueResponse(200, actor).QueueResponse(200, builds).QueueResponse(200, build);

        var spec = await Client(transport).Actor("a1").Server().GetOpenApiSpecAsync();

        Assert.Equal("3.1.0", spec!["openapi"]!.GetValue<string>());
        Assert.Contains("desc=1", transport.Received[1].Uri, StringComparison.Ordinal);
        Assert.Contains("/v2/actor-builds/b2", transport.Received[2].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOpenApiSpecStopsLookingAtOlderBuildsOfTheSameVersion()
    {
        var actor = ServerActor("\"actorStandby\":{\"build\":\"0.0.5\"},\"taggedBuilds\":{}");
        var builds = SpecFixtures.Page(2, SpecFixtures.BuildItem("b4", "0.0.4"), SpecFixtures.BuildItem("b3", "0.0.3"));
        var transport = new MockTransport().QueueResponse(200, actor).QueueResponse(200, builds);

        var ex = await Assert.ThrowsAsync<ActorServerException>(() => Client(transport).Actor("a1").Server().GetOpenApiSpecAsync());

        Assert.Contains("Build 0.0.5 of Actor a1 does not exist", ex.Message, StringComparison.Ordinal);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task GetOpenApiSpecIsNullWhenTheDefinitionHasNoWebServerSchema()
    {
        var actor = ServerActor("\"taggedBuilds\":{\"latest\":{\"buildId\":\"b1\"}}");
        var build = SpecFixtures.Build("b1", extraProperties: "\"actorDefinition\":{}");
        var transport = new MockTransport().QueueResponse(200, actor).QueueResponse(200, build);

        Assert.Null(await Client(transport).Actor("a1").Server().GetOpenApiSpecAsync());
    }

    [Fact]
    public async Task GetOpenApiSpecThrowsWhenTheServingBuildIsMissing()
    {
        var transport = new MockTransport().QueueResponse(200, ServerActor("\"taggedBuilds\":{}"));

        var ex = await Assert.ThrowsAsync<ActorServerException>(() => Client(transport).Actor("a1").Server().GetOpenApiSpecAsync());

        Assert.Contains("Build latest of Actor a1 does not exist", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestForwardsTheOtherContentHeadersWithABodyButNotTheOnesTheClientSets()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(200, "{}");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync(
            "/upload",
            new ActorServerRequestOptions
            {
                Method = HttpMethod.Post,
                Body = new byte[] { 1, 2, 3 },
                Headers = new Dictionary<string, string>
                {
                    ["Content-Disposition"] = "attachment; filename=\"a.bin\"",
                    ["Content-Language"] = "en",
                    ["Content-Length"] = "999",
                    ["X-Plain"] = "yes",
                },
            });

        var request = transport.Received[1];
        Assert.Equal("attachment; filename=\"a.bin\"", request.Header("Content-Disposition"));
        Assert.Equal("en", request.Header("Content-Language"));
        Assert.Equal("yes", request.Header("X-Plain"));
        Assert.NotEqual("999", request.Header("Content-Length"));
        Assert.Equal("application/octet-stream", request.Header("Content-Type"));
    }

    [Fact]
    public async Task RequestWithoutABodyDoesNotSendContentHeadersButKeepsTheOthers()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(200, "{}");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync(
            "/",
            new ActorServerRequestOptions
            {
                Headers = new Dictionary<string, string> { ["Content-Disposition"] = "inline", ["X-Plain"] = "yes" },
            });

        Assert.Equal(string.Empty, transport.Received[1].Header("Content-Disposition"));
        Assert.Equal("yes", transport.Received[1].Header("X-Plain"));
    }

    [Fact]
    public async Task RequestConvertsDatesToIsoUtcAndListsToCommaJoinedValues()
    {
        var transport = new MockTransport().QueueResponse(200, ActorResponse).QueueResponse(200, "{}");

        using var response = await Client(transport).Actor("a1").Server().RequestAsync(
            "/",
            new ActorServerRequestOptions
            {
                Query = new Dictionary<string, object?>
                {
                    ["since"] = new DateTime(2026, 11, 6, 18, 0, 0, DateTimeKind.Utc),
                    ["until"] = new DateTimeOffset(2026, 11, 7, 2, 30, 0, TimeSpan.FromHours(2)),
                    ["plain"] = new DateTime(2026, 11, 6, 18, 0, 0, DateTimeKind.Unspecified),
                    ["tags"] = new[] { "a", "b" },
                    ["ids"] = new List<int> { 1, 2, 3 },
                    ["ratio"] = 1.5,
                },
            });

        var uri = transport.Received[1].Uri;
        Assert.Contains("since=" + Uri.EscapeDataString("2026-11-06T18:00:00.000Z"), uri, StringComparison.Ordinal);
        Assert.Contains("until=" + Uri.EscapeDataString("2026-11-07T00:30:00.000Z"), uri, StringComparison.Ordinal);
        Assert.Contains("plain=" + Uri.EscapeDataString("2026-11-06T18:00:00.000Z"), uri, StringComparison.Ordinal);
        Assert.Contains("tags=" + Uri.EscapeDataString("a,b"), uri, StringComparison.Ordinal);
        Assert.Contains("ids=" + Uri.EscapeDataString("1,2,3"), uri, StringComparison.Ordinal);
        Assert.Contains("ratio=1.5", uri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1.0.99999999999999999999")]
    [InlineData("١.٢.٣")]
    [InlineData("1.2")]
    public async Task GetOpenApiSpecTreatsAnUnparsableBuildReferenceAsAMissingBuild(string buildRef)
    {
        var actor = ServerActor("\"actorStandby\":{\"build\":\"" + buildRef + "\"},\"taggedBuilds\":{}");
        var transport = new MockTransport().QueueResponse(200, actor);

        var ex = await Assert.ThrowsAsync<ActorServerException>(() => Client(transport).Actor("a1").Server().GetOpenApiSpecAsync());

        Assert.Contains("does not exist", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, transport.CallCount);
    }
}
