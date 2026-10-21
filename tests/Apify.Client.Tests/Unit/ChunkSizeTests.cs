using System;
using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Verifies the <c>ChunkSize</c> iteration option, which sizes each page's <c>limit</c> (the smaller of the
/// chunk size and the remaining total <c>Limit</c>) and is never sent as a query parameter itself.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ChunkSizeTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    private static string Page(params string[] ids)
        => "{\"data\":{\"total\":99,\"items\":[" + string.Join(",", Array.ConvertAll(ids, static id => "{\"id\":\"" + id + "\"}")) + "]}}";

    private static async Task DrainAsync<T>(System.Collections.Generic.IAsyncEnumerable<T> items)
    {
        await foreach (var unused in items)
        {
            _ = unused;
        }
    }

    [Fact]
    public async Task CollectionIterationRequestsTheSmallerOfChunkSizeAndRemainingLimit()
    {
        var transport = new MockTransport()
            .QueueResponse(200, Page("a", "b"))
            .QueueResponse(200, Page("c", "d"))
            .QueueResponse(200, Page("e"));

        await DrainAsync(Client(transport).Tasks().IterateAsync(new ListOptions { Limit = 5, ChunkSize = 2 }));

        Assert.Equal(3, transport.CallCount);
        Assert.Contains("limit=2", transport.Received[0].Uri, StringComparison.Ordinal);
        Assert.Contains("limit=2", transport.Received[1].Uri, StringComparison.Ordinal);
        Assert.Contains("limit=1", transport.Received[2].Uri, StringComparison.Ordinal);
        Assert.DoesNotContain("chunkSize", transport.Received[0].Uri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CollectionIterationWithoutLimitUsesChunkSizeAsPageLimit()
    {
        var transport = new MockTransport().QueueResponse(200, Page("a")).QueueResponse(200, Page());

        await DrainAsync(Client(transport).Actors().IterateAsync(new ActorListOptions { ChunkSize = 7 }));

        Assert.All(transport.Received, static r => Assert.Contains("limit=7", r.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZeroLimitDoesNotBeatChunkSize()
    {
        // The API reads limit=0 as unset, so it must lose to the chunk size.
        var transport = new MockTransport().QueueResponse(200, Page("a"));

        await DrainAsync(Client(transport).Schedules().IterateAsync(new ListOptions { Limit = 0, ChunkSize = 3 }));

        Assert.Contains("limit=3", transport.Received[0].Uri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveChunkSizeIsRejectedBeforeAnyRequest(int chunkSize)
    {
        var transport = new MockTransport();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => DrainAsync(Client(transport).Runs().IterateAsync(new ListOptions { ChunkSize = chunkSize })));

        Assert.Equal("ChunkSize", ex.ParamName);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task DatasetItemIterationSizesPagesByChunkSize()
    {
        var headers = new System.Collections.Generic.Dictionary<string, string> { ["X-Apify-Pagination-Total"] = "3" };
        var transport = new MockTransport()
            .QueueResponse(200, "[{\"n\":1},{\"n\":2}]", headers)
            .QueueResponse(200, "[{\"n\":3}]", headers);

        await DrainAsync(Client(transport).Dataset("ds1").IterateItemsAsync(new DatasetListItemsOptions { Limit = 3, ChunkSize = 2 }));

        Assert.Contains("limit=2", transport.Received[0].Uri, StringComparison.Ordinal);
        Assert.Contains("limit=1", transport.Received[1].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyIterationHonoursChunkSizeOverItsOlderName()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[],\"isTruncated\":false}}");

        await DrainAsync(Client(transport).KeyValueStore("s1").IterateKeysAsync(new IterateKeysOptions { ChunkSize = 4, MaxPageLimit = 9 }));

        Assert.Contains("limit=4", transport.Received[0].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestIterationHonoursChunkSize()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[],\"limit\":5}}");

        await DrainAsync(Client(transport).RequestQueue("q1").PaginateRequestsAsync(new PaginateRequestsOptions { ChunkSize = 5 }));

        Assert.Contains("limit=5", transport.Received[0].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreIterationUsesChunkSizeAsPageSize()
    {
        var transport = new MockTransport().QueueResponse(200, Page());

        await DrainAsync(Client(transport).Store().IterateAsync(new StoreListOptions { ChunkSize = 6, Limit = 100 }));

        Assert.Contains("limit=6", transport.Received[0].Uri, StringComparison.Ordinal);
    }
}
