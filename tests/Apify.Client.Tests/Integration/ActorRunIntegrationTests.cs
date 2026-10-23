using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ActorRunIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task ListRuns()
    {
        var client = RequireClient();
        var page = await client.Runs().ListAsync(new ListOptions { Limit = 5 }, new RunListOptions());
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task RunActorAndReadOutputs()
    {
        var client = RequireClient();
        var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);
        Assert.Equal("SUCCEEDED", run.Status);

        Assert.NotNull(await client.Run(run.Id!).GetAsync());

        var log = await client.Run(run.Id!).Log().GetAsync();
        Assert.NotNull(log);
        Assert.NotEqual(string.Empty, log);

        var logBytes = await client.Run(run.Id!).Log().GetBytesAsync();
        Assert.NotNull(logBytes);
        Assert.Equal(log, System.Text.Encoding.UTF8.GetString(logBytes!));

        await client.Run(run.Id!).Dataset().ListItemsAsync(new DatasetListItemsOptions());
        await client.Run(run.Id!).KeyValueStore().GetRecordAsync("OUTPUT");
    }

    [SkippableFact]
    public async Task IterateRunDatasetItems()
    {
        var client = RequireClient();
        var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);

        var iterated = new List<string>();
        await foreach (var item in client.Run(run.Id!).IterateDatasetItemsAsync(new RunIterateDatasetItemsOptions { ChunkSize = 1 }))
        {
            Assert.NotNull(item);
            iterated.Add(item!.ToJsonString());
        }

        // hello-world pushes an item, so an iterator that yields nothing is a failure.
        Assert.NotEmpty(iterated);

        // The run is finished, so its dataset is complete. The dataset listing lags the pushed items by a few
        // seconds (which is why the iterator drains past the reported item count), so it must catch up to
        // exactly the items the iterator already read.
        var listed = new List<string>();
        Assert.True(
            await PollUntilAsync(
                EventualConsistencyAttempts,
                EventualConsistencyBackoff,
                async () =>
                {
                    var page = await client.Run(run.Id!).Dataset().ListItemsAsync(new DatasetListItemsOptions());
                    listed = page.Items.Select(static i => i!.ToJsonString()).ToList();
                    return listed.Count == iterated.Count;
                }),
            "expected the dataset listing to catch up with the iterated items");
        Assert.Equal(listed, iterated);
    }

    [SkippableFact]
    public async Task UpdateAndDeleteRun()
    {
        var client = RequireClient();

        // Start a run and wait for it to reach a terminal state before mutating it.
        var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);
        Assert.Equal("SUCCEEDED", run.Status);

        var statusMessage = "updated by dotnet client integration test";
        var updated = await client.Run(run.Id!).UpdateAsync(new { statusMessage });
        Assert.Equal(run.Id, updated.Id);
        Assert.Equal(statusMessage, updated.StatusMessage);

        // Delete the run; DeleteAsync throws on a non-success status, so a clean return is the check.
        // (No read-after-delete assertion — that would rely on strong replica consistency and could flake.)
        await client.Run(run.Id!).DeleteAsync();
    }

    [SkippableFact]
    public async Task LastRunAccess()
    {
        var client = RequireClient();
        await client.Actor("apify/hello-world").CallAsync(null, null, 120);

        var lastRun = await client.Actor("apify/hello-world").LastRun(new LastRunOptions { Status = "SUCCEEDED" }).GetAsync();
        Assert.NotNull(lastRun);
        Assert.Equal("SUCCEEDED", lastRun!.Status);

        var byOrigin = await client.Actor("apify/hello-world")
            .LastRun(new LastRunOptions { Status = "SUCCEEDED", Origin = "API" })
            .GetAsync();
        Assert.NotNull(byOrigin);
        Assert.Equal("SUCCEEDED", byOrigin!.Status);
    }
}
