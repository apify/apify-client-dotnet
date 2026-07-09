using System.Threading.Tasks;
using Apify.Client.Models;
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
        Assert.Equal(ActorJobStatus.Succeeded, run.Status);

        Assert.NotNull(await client.Run(run.Id!).GetAsync());

        var log = await client.Run(run.Id!).Log().GetAsync();
        Assert.NotNull(log);
        Assert.NotEqual(string.Empty, log);

        await client.Run(run.Id!).Dataset().ListItemsAsync(new DatasetListItemsOptions());
        await client.Run(run.Id!).KeyValueStore().GetRecordAsync("OUTPUT");
    }

    [SkippableFact]
    public async Task UpdateAndDeleteRun()
    {
        var client = RequireClient();

        // Start a run and wait for it to reach a terminal state before mutating it.
        var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);
        Assert.Equal(ActorJobStatus.Succeeded, run.Status);

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

        var lastRun = await client.Actor("apify/hello-world").LastRun(new LastRunOptions { Status = ActorJobStatus.Succeeded }).GetAsync();
        Assert.NotNull(lastRun);
        Assert.Equal(ActorJobStatus.Succeeded, lastRun!.Status);

        var byOrigin = await client.Actor("apify/hello-world")
            .LastRun(new LastRunOptions { Status = ActorJobStatus.Succeeded, Origin = RunOrigin.Api })
            .GetAsync();
        Assert.NotNull(byOrigin);
        Assert.Equal(ActorJobStatus.Succeeded, byOrigin!.Status);
    }
}
