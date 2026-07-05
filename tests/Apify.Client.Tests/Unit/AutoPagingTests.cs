using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Offline tests for the auto-paging iterators: they must walk pages by offset until the reported total is
/// reached, sending the right per-page <c>offset</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AutoPagingTests
{
    private static readonly IReadOnlyDictionary<string, string> Page1Headers = new Dictionary<string, string>
    {
        ["X-Apify-Pagination-Total"] = "3",
        ["X-Apify-Pagination-Offset"] = "0",
        ["X-Apify-Pagination-Limit"] = "2",
    };

    private static readonly IReadOnlyDictionary<string, string> Page2Headers = new Dictionary<string, string>
    {
        ["X-Apify-Pagination-Total"] = "3",
        ["X-Apify-Pagination-Offset"] = "2",
        ["X-Apify-Pagination-Limit"] = "1",
    };

    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task CollectionIterateWalksAllPagesByOffset()
    {
        var transport = new MockTransport()
            .QueueResponse(200, "{\"data\":{\"total\":3,\"items\":[{\"id\":\"a\"},{\"id\":\"b\"}]}}")
            .QueueResponse(200, "{\"data\":{\"total\":3,\"items\":[{\"id\":\"c\"}]}}");

        var ids = new List<string?>();
        await foreach (var actor in Client(transport).Actors().IterateAsync())
        {
            ids.Add(actor.Id);
        }

        Assert.Equal(new[] { "a", "b", "c" }, ids);
        Assert.Equal(2, transport.CallCount);
        Assert.Contains("offset=0", transport.Received[0].Uri, StringComparison.Ordinal);
        Assert.Contains("offset=2", transport.Received[1].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CollectionIterateStopsAtLimit()
    {
        var transport = new MockTransport()
            .QueueResponse(200, "{\"data\":{\"total\":10,\"items\":[{\"id\":\"a\"},{\"id\":\"b\"}]}}");

        var ids = new List<string?>();
        await foreach (var actor in Client(transport).Actors().IterateAsync(new ActorListOptions { Limit = 2 }))
        {
            ids.Add(actor.Id);
        }

        // Limit=2 is satisfied by the first page, so no second page is fetched even though total is 10.
        Assert.Equal(new[] { "a", "b" }, ids);
        Assert.Equal(1, transport.CallCount);
        Assert.Contains("limit=2", transport.Received[0].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DatasetIterateItemsWalksAllPagesByOffset()
    {
        var transport = new MockTransport()
            .QueueResponse(200, "[{\"i\":1},{\"i\":2}]", Page1Headers)
            .QueueResponse(200, "[{\"i\":3}]", Page2Headers);

        var values = new List<int>();
        await foreach (var item in Client(transport).Dataset("ds1").IterateItemsAsync())
        {
            values.Add(item!["i"]!.GetValue<int>());
        }

        Assert.Equal(new[] { 1, 2, 3 }, values);
        Assert.Equal(2, transport.CallCount);
        Assert.Contains("/datasets/ds1/items", transport.Received[0].Uri, StringComparison.Ordinal);
        Assert.Contains("offset=0", transport.Received[0].Uri, StringComparison.Ordinal);
        Assert.Contains("offset=2", transport.Received[1].Uri, StringComparison.Ordinal);
    }
}
