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

        await client.Run(run.Id!).Dataset().ListItemsAsync(new DatasetListItemsOptions());
        await client.Run(run.Id!).KeyValueStore().GetRecordAsync("OUTPUT");
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
