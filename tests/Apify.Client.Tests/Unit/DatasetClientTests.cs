using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class DatasetClientTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task StreamItemsUsesFormatQueryAndReturnsReadableStream()
    {
        var transport = new MockTransport().QueueResponse(200, "a,b\n1,2\n");
        using var stream = await Client(transport).Dataset("ds1").StreamItemsAsync(DownloadItemsFormat.Csv, new DatasetDownloadOptions { Bom = true });

        var uri = transport.LastRequest.Uri;
        Assert.Contains("/datasets/ds1/items", uri, StringComparison.Ordinal);
        Assert.Contains("format=csv", uri, StringComparison.Ordinal);
        Assert.Contains("bom=1", uri, StringComparison.Ordinal);

        using var reader = new StreamReader(stream);
        Assert.Equal("a,b\n1,2\n", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task StreamItemsThrowsApifyApiExceptionOnError()
    {
        var transport = new MockTransport().QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"no dataset\"}}");
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => Client(transport).Dataset("missing").StreamItemsAsync(DownloadItemsFormat.Json));

        // Still catchable through the common base, matching every other client-raised error.
        Assert.IsAssignableFrom<ApifyClientException>(ex);
    }

    [Fact]
    public async Task ListItemsCountIsTheRowsScannedWhenAFilterReturnsFewerItems()
    {
        // `skipEmpty` applies after offset/limit, so a page can scan more rows than it returns items.
        var headers = new Dictionary<string, string>
        {
            ["X-Apify-Pagination-Total"] = "10",
            ["X-Apify-Pagination-Offset"] = "0",
            ["X-Apify-Pagination-Limit"] = "4",
            ["X-Apify-Pagination-Count"] = "4",
        };
        var transport = new MockTransport().QueueResponse(200, "[{\"i\":1}]", headers);

        var page = await Client(transport).Dataset("ds1").ListItemsAsync(new DatasetListItemsOptions { SkipEmpty = true, Limit = 4 });

        Assert.Single(page.Items);
        // `Count` is the rows scanned, which a filter leaves above the items returned; the list's own
        // enumeration and indexer still cover the items only.
        Assert.Equal(4, page.Count);
        Assert.Single((IEnumerable<System.Text.Json.Nodes.JsonNode?>)page);
        Assert.Equal(page.Items.Count, ((IReadOnlyCollection<System.Text.Json.Nodes.JsonNode?>)page).Count);
        Assert.Equal(10, page.Total);
    }

    [Theory]
    [InlineData("3", 1, 3)] // filtered page: scanned rows above the items returned
    [InlineData("0", 2, 2)] // lagging header: the items returned
    [InlineData("1", 2, 2)] // unwound page: more items than the rows scanned
    [InlineData(null, 1, 1)] // header absent: the items returned
    public async Task ListItemsCountIsTheLargerOfTheScannedRowsAndTheItemsReturned(string? scanned, int returned, long expectedCount)
    {
        var headers = new Dictionary<string, string>();
        if (scanned is not null)
        {
            headers["X-Apify-Pagination-Count"] = scanned;
        }

        var body = "[" + string.Join(",", Enumerable.Range(1, returned).Select(static n => "{\"a\":" + n + "}")) + "]";
        var transport = new MockTransport().QueueResponse(200, body, headers);

        var page = await Client(transport).Dataset("ds1").ListItemsAsync(new DatasetListItemsOptions { Limit = 3 });

        Assert.Equal(expectedCount, page.Count);
        Assert.Equal(returned, page.Items.Count);
    }

    [Fact]
    public async Task ListingPagesHaveTheCountTheApiReports()
    {
        // The Store listing drops entries after counting them, so `count` can exceed the items returned.
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.PageWithCount(2, 3, SpecFixtures.ActorItem("a1"), SpecFixtures.ActorItem("a2")));

        var page = await Client(transport).Actors().ListAsync();

        Assert.Equal(3, page.Count);
        Assert.Equal(2, page.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateItemsPublicUrlRejectsANonPositiveExpiryBeforeAnyRequest(int expiresInSecs)
    {
        var transport = new MockTransport();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Client(transport).Dataset("ds1").CreateItemsPublicUrlAsync(expiresInSecs: expiresInSecs));

        Assert.Equal(0, transport.CallCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateKeysPublicUrlRejectsANonPositiveExpiryBeforeAnyRequest(int expiresInSecs)
    {
        var transport = new MockTransport();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => Client(transport).KeyValueStore("kvs1").CreateKeysPublicUrlAsync(expiresInSecs: expiresInSecs));

        Assert.Equal(0, transport.CallCount);
    }
}
