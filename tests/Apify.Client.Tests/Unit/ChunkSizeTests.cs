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
            .QueueResponse(200, SpecFixtures.Page(5, SpecFixtures.TaskItem("a"), SpecFixtures.TaskItem("b")))
            .QueueResponse(200, SpecFixtures.Page(5, SpecFixtures.TaskItem("c"), SpecFixtures.TaskItem("d")))
            .QueueResponse(200, SpecFixtures.Page(5, SpecFixtures.TaskItem("e")));

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
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.ActorPage(1, "a")).QueueResponse(200, SpecFixtures.ActorPage(1));

        await DrainAsync(Client(transport).Actors().IterateAsync(new ActorListOptions { ChunkSize = 7 }));

        Assert.All(transport.Received, static r => Assert.Contains("limit=7", r.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZeroLimitDoesNotBeatChunkSize()
    {
        // The API reads limit=0 as unset, so it must lose to the chunk size.
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.Page(1, SpecFixtures.ScheduleItem("a"))).QueueResponse(200, SpecFixtures.Page(1));

        await DrainAsync(Client(transport).Schedules().IterateAsync(new ListOptions { Limit = 0, ChunkSize = 3 }));

        Assert.All(transport.Received, static r => Assert.Contains("limit=3", r.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ZeroLimitMeansNoLimitForCollectionStoreAndDatasetItemIteration()
    {
        var collection = new MockTransport().QueueResponse(200, SpecFixtures.ActorPage(3, "a", "b")).QueueResponse(200, SpecFixtures.ActorPage(3, "c")).QueueResponse(200, SpecFixtures.ActorPage(3));
        var collected = 0;
        await foreach (var unused in Client(collection).Actors().IterateAsync(new ActorListOptions { Limit = 0, ChunkSize = 2 }))
        {
            _ = unused;
            collected++;
        }

        Assert.Equal(3, collected);

        var store = new MockTransport().QueueResponse(200, SpecFixtures.Page(2, SpecFixtures.StoreItem("a"))).QueueResponse(200, SpecFixtures.Page(2, SpecFixtures.StoreItem("b"))).QueueResponse(200, SpecFixtures.Page(2));
        var stored = 0;
        await foreach (var unused in Client(store).Store().IterateAsync(new StoreListOptions { Limit = 0, ChunkSize = 1 }))
        {
            _ = unused;
            stored++;
        }

        Assert.Equal(2, stored);

        var dataset = new MockTransport().QueueResponse(200, "[{\"n\":1},{\"n\":2}]").QueueResponse(200, "[{\"n\":3}]").QueueResponse(200, "[]");
        var items = 0;
        await foreach (var unused in Client(dataset).Dataset("ds1").IterateItemsAsync(new DatasetListItemsOptions { Limit = 0, ChunkSize = 2 }))
        {
            _ = unused;
            items++;
        }

        Assert.Equal(3, items);
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
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.KeysPage(4, false, null));

        await DrainAsync(Client(transport).KeyValueStore("s1").IterateKeysAsync(new IterateKeysOptions { ChunkSize = 4, MaxPageLimit = 9 }));

        Assert.Contains("limit=4", transport.Received[0].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestIterationHonoursChunkSize()
    {
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.ListRequestsPage(5));

        await DrainAsync(Client(transport).RequestQueue("q1").PaginateRequestsAsync(new PaginateRequestsOptions { ChunkSize = 5 }));

        Assert.Contains("limit=5", transport.Received[0].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreIterationSizesPagesByChunkSizeAndCapsTotalAtLimit()
    {
        var transport = new MockTransport()
            .QueueResponse(200, SpecFixtures.Page(9, SpecFixtures.StoreItem("a"), SpecFixtures.StoreItem("b")))
            .QueueResponse(200, SpecFixtures.Page(9, SpecFixtures.StoreItem("c")));

        var ids = new System.Collections.Generic.List<string?>();
        await foreach (var item in Client(transport).Store().IterateAsync(new StoreListOptions { ChunkSize = 2, Limit = 3 }))
        {
            ids.Add(item.Id);
        }

        Assert.Equal(new[] { "a", "b", "c" }, ids);
        Assert.Equal(2, transport.CallCount);
        Assert.Contains("limit=2", transport.Received[0].Uri, StringComparison.Ordinal);
        Assert.Contains("limit=1", transport.Received[1].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreIterationAdvancesByTheRowsScannedNotTheItemsReturned()
    {
        // The Store listing drops Actors the caller cannot run after applying offset and limit, so a page that
        // scanned 3 rows can return only 2 Actors. The next page must start after all 3 rows.
        var transport = new MockTransport()
            .QueueResponse(200, SpecFixtures.PageWithCount(9, 3, SpecFixtures.StoreItem("a"), SpecFixtures.StoreItem("b")))
            .QueueResponse(200, SpecFixtures.Page(9));

        await DrainAsync(Client(transport).Store().IterateAsync(new StoreListOptions { ChunkSize = 3 }));

        Assert.Contains("offset=3", transport.Received[1].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreIterationContinuesPastAPageThatScannedRowsButReturnedNoItems()
    {
        // Every Actor of the first page was dropped (count 1, no items); the next page still has one.
        var transport = new MockTransport()
            .QueueResponse(200, SpecFixtures.PageWithCount(9, 1))
            .QueueResponse(200, SpecFixtures.Page(9, SpecFixtures.StoreItem("a")))
            .QueueResponse(200, SpecFixtures.Page(9));

        var ids = new System.Collections.Generic.List<string?>();
        await foreach (var item in Client(transport).Store().IterateAsync(new StoreListOptions { ChunkSize = 1 }))
        {
            ids.Add(item.Id);
        }

        Assert.Equal(new[] { "a" }, ids);
        Assert.Contains("offset=1", transport.Received[1].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreLimitCountsTheActorsYieldedEvenWhenAPageDropsSome()
    {
        // Limit = 3 asks for three Actors. The first page scanned 3 rows but returned only 2 (one was
        // dropped), so one more Actor is still wanted, from the row after the 3 scanned.
        var transport = new MockTransport()
            .QueueResponse(200, SpecFixtures.PageWithCount(9, 3, SpecFixtures.StoreItem("a"), SpecFixtures.StoreItem("b")))
            .QueueResponse(200, SpecFixtures.Page(9, SpecFixtures.StoreItem("c")));

        var ids = new System.Collections.Generic.List<string?>();
        await foreach (var item in Client(transport).Store().IterateAsync(new StoreListOptions { Limit = 3, ChunkSize = 3 }))
        {
            ids.Add(item.Id);
        }

        Assert.Equal(new[] { "a", "b", "c" }, ids);
        Assert.Equal(2, transport.CallCount);
        Assert.Contains("offset=3", transport.Received[1].Uri, StringComparison.Ordinal);
        Assert.Contains("limit=1", transport.Received[1].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScannedRowsAreCappedAtTheRequestedLimit()
    {
        // Limit = 2 asks for 2 rows; a page claiming 5 scanned rows must not advance past what was asked for.
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.PageWithCount(9, 5, SpecFixtures.ActorItem("a"), SpecFixtures.ActorItem("b")));

        await DrainAsync(Client(transport).Actors().IterateAsync(new ActorListOptions { Limit = 2 }));

        Assert.Equal(1, transport.CallCount);
    }

    [Theory]
    [InlineData("Offset")]
    [InlineData("Limit")]
    public async Task NegativeOffsetOrLimitIsRejectedBeforeAnyRequest(string option)
    {
        var transport = new MockTransport();
        var options = option == "Offset" ? new ActorListOptions { Offset = -1 } : new ActorListOptions { Limit = -1 };

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => DrainAsync(Client(transport).Actors().IterateAsync(options)));

        Assert.Equal(option, ex.ParamName);
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task StoreIterationLimitIsATotalCapNotAPageSize()
    {
        // Limit = 2 is satisfied by the first page, so nothing more may be requested.
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.Page(50, SpecFixtures.StoreItem("a"), SpecFixtures.StoreItem("b")));

        var count = 0;
        await foreach (var unused in Client(transport).Store().IterateAsync(new StoreListOptions { Limit = 2 }))
        {
            _ = unused;
            count++;
        }

        Assert.Equal(2, count);
        Assert.Equal(1, transport.CallCount);
        Assert.Contains("limit=2", transport.Received[0].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StoreIterationDoesNotStopAtAnEarlierPagesStaleTotal()
    {
        // The Store grows between pages: page 1 reports a total of 2, but a third item exists on page 2.
        var transport = new MockTransport()
            .QueueResponse(200, SpecFixtures.Page(2, SpecFixtures.StoreItem("a"), SpecFixtures.StoreItem("b")))
            .QueueResponse(200, SpecFixtures.Page(3, SpecFixtures.StoreItem("c")))
            .QueueResponse(200, SpecFixtures.Page(3));

        var count = 0;
        await foreach (var unused in Client(transport).Store().IterateAsync())
        {
            _ = unused;
            count++;
        }

        Assert.Equal(3, count);
    }
}
