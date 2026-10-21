using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Verifies <c>RunClient.IterateDatasetItemsAsync</c>: it reads a run's dataset while the run is still
/// producing items, pages by <c>chunkSize</c> up to <c>itemCount</c>, and drains rows past a lagging
/// <c>itemCount</c> once the run is finished.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RunIterateDatasetItemsTests
{
    private const string FinishedRun = "{\"data\":{\"id\":\"run1\",\"status\":\"SUCCEEDED\"}}";
    private const string RunningRun = "{\"data\":{\"id\":\"run1\",\"status\":\"RUNNING\"}}";

    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    private static string DatasetWithItemCount(int itemCount) => "{\"data\":{\"id\":\"ds1\",\"itemCount\":" + itemCount + "}}";

    private static string Items(params int[] ids) => "[" + string.Join(",", ids.Select(static i => "{\"n\":" + i + "}")) + "]";

    private static async Task<List<int>> CollectAsync(IAsyncEnumerable<JsonNode?> items)
    {
        var result = new List<int>();
        await foreach (var item in items)
        {
            result.Add(item!["n"]!.GetValue<int>());
        }

        return result;
    }

    [Fact]
    public async Task FinishedRunPagesByChunkSizeAndDrainsRowsPastLaggingItemCount()
    {
        var transport = new MockTransport()
            .QueueResponse(200, FinishedRun)
            .QueueResponse(200, DatasetWithItemCount(3))
            .QueueResponse(200, Items(1, 2))
            .QueueResponse(200, Items(3))
            .QueueResponse(200, Items(4)) // past itemCount: the count lags the pushed items
            .QueueResponse(200, Items());

        var items = await CollectAsync(Client(transport).Run("run1").IterateDatasetItemsAsync(new RunIterateDatasetItemsOptions { ChunkSize = 2 }));

        Assert.Equal(new[] { 1, 2, 3, 4 }, items);
        var uris = transport.Received.Select(static r => r.Uri).ToList();
        Assert.Contains("offset=0", uris[2], StringComparison.Ordinal);
        Assert.Contains("limit=2", uris[2], StringComparison.Ordinal);
        Assert.Contains("offset=2", uris[3], StringComparison.Ordinal);
        Assert.Contains("limit=1", uris[3], StringComparison.Ordinal);
        Assert.Contains("offset=3", uris[4], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunningRunPollsForFinishAndReadsRowsAsTheyAppear()
    {
        var transport = new MockTransport()
            .QueueResponse(200, RunningRun)
            .QueueResponse(200, DatasetWithItemCount(1))
            .QueueResponse(200, Items(1))
            .QueueResponse(200, FinishedRun) // waitForFinish poll
            .QueueResponse(200, DatasetWithItemCount(2))
            .QueueResponse(200, Items(2))
            .QueueResponse(200, Items());

        var items = await CollectAsync(Client(transport).Run("run1").IterateDatasetItemsAsync(new RunIterateDatasetItemsOptions { PollIntervalSecs = 1 }));

        Assert.Equal(new[] { 1, 2 }, items);
        Assert.Contains("waitForFinish=", transport.Received[3].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LimitBoundsScannedRowsAndStopsWithoutFurtherReads()
    {
        var transport = new MockTransport()
            .QueueResponse(200, FinishedRun)
            .QueueResponse(200, DatasetWithItemCount(10))
            .QueueResponse(200, Items(2, 3));

        var items = await CollectAsync(Client(transport).Run("run1").IterateDatasetItemsAsync(new RunIterateDatasetItemsOptions { Offset = 1, Limit = 2 }));

        Assert.Equal(new[] { 2, 3 }, items);
        Assert.Equal(3, transport.CallCount);
        Assert.Contains("offset=1", transport.LastRequest.Uri, StringComparison.Ordinal);
        Assert.Contains("limit=2", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LastRunClientPinsToTheRunItFirstResolved()
    {
        var transport = new MockTransport()
            .QueueResponse(200, "{\"data\":{\"id\":\"resolvedRun\",\"status\":\"SUCCEEDED\"}}")
            .QueueResponse(200, DatasetWithItemCount(1))
            .QueueResponse(200, Items(1))
            .QueueResponse(200, Items());

        var items = await CollectAsync(Client(transport).Actor("me~act").LastRun().IterateDatasetItemsAsync());

        Assert.Equal(new[] { 1 }, items);
        Assert.Contains("/actors/me~act/runs/last", transport.Received[0].Uri, StringComparison.Ordinal);
        Assert.Contains("/actor-runs/resolvedRun/dataset", transport.Received[1].Uri, StringComparison.Ordinal);
        Assert.Contains("/actor-runs/resolvedRun/dataset/items", transport.Received[2].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FilteredEmptyTailIsConfirmedWithAPlainRead()
    {
        var transport = new MockTransport()
            .QueueResponse(200, FinishedRun)
            .QueueResponse(200, DatasetWithItemCount(0))
            .QueueResponse(200, Items()) // filtered page: empty, nothing scanned
            .QueueResponse(200, "[{}]") // plain read: a row the filter dropped, so iteration must go on
            .QueueResponse(200, Items()) // next filtered page
            .QueueResponse(200, Items()); // plain read: nothing left

        var items = await CollectAsync(Client(transport).Run("run1").IterateDatasetItemsAsync(new RunIterateDatasetItemsOptions { SkipEmpty = true }));

        Assert.Empty(items);
        Assert.Equal(6, transport.CallCount);
        Assert.Contains("skipEmpty=1", transport.Received[2].Uri, StringComparison.Ordinal);
        Assert.Contains("offset=0", transport.Received[3].Uri, StringComparison.Ordinal);
        Assert.DoesNotContain("skipEmpty", transport.Received[3].Uri, StringComparison.Ordinal);
        Assert.Contains("offset=1000", transport.Received[4].Uri, StringComparison.Ordinal);
        Assert.Contains("offset=1000", transport.Received[5].Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OffsetPlusLimitBeyondIntRangeStillIteratesAllRows()
    {
        var transport = new MockTransport()
            .QueueResponse(200, FinishedRun)
            .QueueResponse(200, DatasetWithItemCount(3))
            .QueueResponse(200, Items(2, 3))
            .QueueResponse(200, Items());

        var items = await CollectAsync(Client(transport).Run("run1").IterateDatasetItemsAsync(new RunIterateDatasetItemsOptions { Offset = 1, Limit = int.MaxValue }));

        Assert.Equal(new[] { 2, 3 }, items);
    }

    [Fact]
    public async Task RunTimeoutAppliesToTheDatasetRequests()
    {
        var transport = new MockTransport()
            .QueueResponse(200, FinishedRun)
            .QueueResponse(200, DatasetWithItemCount(1))
            .QueueResponse(200, Items(1))
            .QueueResponse(200, Items());

        await CollectAsync(Client(transport).Run("run1").WithTimeout(TimeSpan.FromSeconds(3)).IterateDatasetItemsAsync());

        Assert.All(transport.Timeouts, static t => Assert.Equal(3, t));
    }

    [Theory]
    [InlineData("Offset", -1)]
    [InlineData("Limit", -1)]
    [InlineData("PollIntervalSecs", -1)]
    [InlineData("ChunkSize", 0)]
    [InlineData("ChunkSize", -1)]
    public async Task InvalidArgumentsAreRejectedNamingTheOption(string option, int value)
    {
        var options = option switch
        {
            "Offset" => new RunIterateDatasetItemsOptions { Offset = value },
            "Limit" => new RunIterateDatasetItemsOptions { Limit = value },
            "PollIntervalSecs" => new RunIterateDatasetItemsOptions { PollIntervalSecs = value },
            _ => new RunIterateDatasetItemsOptions { ChunkSize = value },
        };
        var transport = new MockTransport();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CollectAsync(Client(transport).Run("run1").IterateDatasetItemsAsync(options)));

        Assert.Equal(option, ex.ParamName);
        Assert.Equal(0, transport.CallCount);
    }
}
