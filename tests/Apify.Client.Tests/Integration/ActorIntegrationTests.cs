using System.Threading.Tasks;
using Apify.Client.Models;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ActorIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task ListActors()
    {
        var client = RequireClient();
        var page = await client.Actors().ListAsync(new ActorListOptions { My = true, Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task GetActor()
    {
        var client = RequireClient();
        var created = await client.Actors().CreateAsync(MinimalActor(UniqueName("get")));
        try
        {
            var got = await client.Actor(created.Id!).GetAsync();
            Assert.NotNull(got);
            Assert.Equal(created.Id, got!.Id);
        }
        finally
        {
            await client.Actor(created.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task ActorCrudFlow()
    {
        var client = RequireClient();
        var created = await client.Actors().CreateAsync(MinimalActor(UniqueName("crud")));
        try
        {
            var actor = client.Actor(created.Id!);
            Assert.NotNull(await actor.GetAsync());
            var updated = await actor.UpdateAsync(new { title = "Updated Title" });
            Assert.Equal("Updated Title", updated.Title);
            await actor.Builds().ListAsync(new ListOptions());
            await actor.Versions().ListAsync(new ListOptions());
        }
        finally
        {
            await client.Actor(created.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task ActorVersionCrudFlow()
    {
        var client = RequireClient();
        var created = await client.Actors().CreateAsync(MinimalActor(UniqueName("ver")));
        try
        {
            var actor = client.Actor(created.Id!);
            var version = await actor.Versions().CreateAsync(new
            {
                versionNumber = "0.1",
                sourceType = "SOURCE_FILES",
                buildTag = "latest",
                sourceFiles = System.Array.Empty<object>(),
            });
            Assert.Equal("0.1", version.VersionNumber);
            Assert.NotNull(await actor.Version("0.1").GetAsync());
            await actor.Versions().ListAsync(new ListOptions());
            await actor.Version("0.1").UpdateAsync(new
            {
                buildTag = "beta",
                sourceType = "SOURCE_FILES",
                sourceFiles = System.Array.Empty<object>(),
            });
            await actor.Version("0.1").DeleteAsync();
        }
        finally
        {
            await client.Actor(created.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task ValidateInput()
    {
        var client = RequireClient();
        // apify/hello-world is a public store Actor; validate-input is read-only and returns
        // {"valid": <bool>}. A well-formed input validates true.
        Assert.True(await client.Actor("apify/hello-world").ValidateInputAsync(new { firstNumber = 1 }));
    }

    [SkippableFact]
    public async Task ActorEnvVarCrudFlow()
    {
        var client = RequireClient();
        var created = await client.Actors().CreateAsync(MinimalActor(UniqueName("env")));
        try
        {
            var actor = client.Actor(created.Id!);
            var envVars = actor.Version("0.0").EnvVars();
            await envVars.CreateAsync(new ActorEnvVar("MY_VAR", "value1"));
            Assert.NotNull(await actor.Version("0.0").EnvVar("MY_VAR").GetAsync());
            await envVars.ListAsync();
            await actor.Version("0.0").EnvVar("MY_VAR").UpdateAsync(new ActorEnvVar("MY_VAR", "value2"));
            await actor.Version("0.0").EnvVar("MY_VAR").DeleteAsync();
        }
        finally
        {
            await client.Actor(created.Id!).DeleteAsync();
        }
    }
}
