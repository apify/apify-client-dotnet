using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class BuildIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task ListBuilds()
    {
        var client = RequireClient();
        var page = await client.Builds().ListAsync(new ListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task DefaultBuild()
    {
        var client = RequireClient();
        // A public Store Actor always has a default build; resolve it and confirm the build is fetchable.
        var buildClient = await client.Actor("apify/hello-world").DefaultBuildAsync();
        var build = await buildClient.GetAsync();
        Assert.NotNull(build);
        Assert.NotNull(build!.Id);
    }

    [SkippableFact]
    public async Task BuildActorFlow()
    {
        var client = RequireClient();
        var created = await client.Actors().CreateAsync(MinimalActor(UniqueName("build")));
        try
        {
            var build = await client.Actor(created.Id!).BuildAsync("0.0", new ActorBuildOptions());
            var finished = await client.Build(build.Id!).WaitForFinishAsync(300);
            Assert.True(finished.IsTerminal, "build did not finish: " + finished.Status);

            Assert.NotNull(await client.Build(build.Id!).GetAsync());
            await client.Build(build.Id!).Log().GetAsync();
            await client.Build(build.Id!).GetOpenApiDefinitionAsync();
        }
        finally
        {
            await client.Actor(created.Id!).DeleteAsync();
        }
    }
}
