using System;
using System.Threading.Tasks;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Caller-supplied values interpolated into a URL path (resource ids, record keys, request IDs) must not be
/// able to restructure the request path, matching the reference client's URL path encoding.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PathSegmentTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task RecordKeyIsEncodedInsteadOfRestructuringThePath()
    {
        var transport = new MockTransport().QueueResponse(200, "value").QueueResponse(200, "value").QueueResponse(200, "value");
        var store = Client(transport).KeyValueStore("STORE");

        await store.GetRecordAsync("../../actor-runs/VICTIM/abort");
        Assert.Contains("/key-value-stores/STORE/records/..%2F..%2Factor-runs%2FVICTIM%2Fabort?", transport.LastRequest.Uri, StringComparison.Ordinal);

        await store.GetRecordAsync("foo?injected=1");
        Assert.Contains("/records/foo%3Finjected%3D1?", transport.LastRequest.Uri, StringComparison.Ordinal);

        await store.GetRecordAsync("foo#frag");
        Assert.Contains("/records/foo%23frag?", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestIdIsEncoded()
    {
        var transport = new MockTransport().QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"m\"}}");

        await Client(transport).RequestQueue("QUEUE").GetRequestAsync("../../../datasets/V/items");

        Assert.EndsWith("/request-queues/QUEUE/requests/..%2F..%2F..%2Fdatasets%2FV%2Fitems", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EverySlashOfAResourceIdIsReplacedWithATilde()
    {
        var transport = new MockTransport().QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"m\"}}");

        await Client(transport).Actor("user/name/extra").GetAsync();

        Assert.EndsWith("/actors/user~name~extra", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ResourceIdIsPercentEncoded()
    {
        var transport = new MockTransport().QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"m\"}}");

        await Client(transport).Dataset("ds?x=1#y").GetAsync();

        Assert.EndsWith("/datasets/ds%3Fx%3D1%23y", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LiteralPathSeparatorsOfFixedSubPathsAreKept()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"abc\",\"lockExpiresAt\":\"2026-01-01T00:00:00.000Z\"}}");

        await Client(transport).RequestQueue("QUEUE").ProlongRequestLockAsync("abc", 1);

        Assert.Contains("/request-queues/QUEUE/requests/abc/lock?", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public async Task DotSegmentsAreRejected(string segment)
    {
        var transport = new MockTransport();
        var client = Client(transport);

        await Assert.ThrowsAsync<ArgumentException>(() => client.KeyValueStore("STORE").GetRecordAsync(segment));
        await Assert.ThrowsAsync<ArgumentException>(() => client.RequestQueue("QUEUE").GetRequestAsync(segment));
        Assert.Throws<ArgumentException>(() => client.Dataset(segment));
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task EmptyRecordKeyAndRequestIdAreRejected()
    {
        var transport = new MockTransport();
        var client = Client(transport);

        await Assert.ThrowsAsync<ArgumentException>(() => client.KeyValueStore("STORE").DeleteRecordAsync(string.Empty));
        await Assert.ThrowsAsync<ArgumentException>(() => client.RequestQueue("QUEUE").DeleteRequestAsync(string.Empty));
        Assert.Equal(0, transport.CallCount);
    }
}
