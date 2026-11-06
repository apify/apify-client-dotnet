using System;
using System.Collections.Generic;
using System.IO;
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
    public async Task ListItemsReportsTheRowsScannedAsTheCountOfThePage()
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
        Assert.Equal(4, page.Count);
        Assert.Equal(10, page.Total);
    }

    [Fact]
    public async Task ListItemsCountIsTheReturnedItemsWhenTheHeaderIsAbsentOrLower()
    {
        var lagging = new Dictionary<string, string> { ["X-Apify-Pagination-Count"] = "0" };
        var transport = new MockTransport()
            .QueueResponse(200, "[{\"i\":1},{\"i\":2}]", lagging)
            .QueueResponse(200, "[{\"i\":1},{\"i\":2},{\"i\":3}]");
        var dataset = Client(transport).Dataset("ds1");

        Assert.Equal(2, (await dataset.ListItemsAsync()).Count);
        Assert.Equal(3, (await dataset.ListItemsAsync()).Count);
    }
}
