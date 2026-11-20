using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ActorRunIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task ListRunsByDeployment()
    {
        var client = RequireClient();
        var page = await client.Runs().ListAsync(
            new ListOptions { Limit = 5 },
            new RunListOptions { Status = new[] { "READY", "RUNNING" }, Deployment = new[] { "SINGLE_TENANT" } });

        Assert.All(page.Items, static run =>
        {
            Assert.Contains(run.Status, new[] { "READY", "RUNNING" });
            Assert.Equal("SINGLE_TENANT", run.Standby?["deployment"]?.GetValue<string>());
        });
    }

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

        // The dataset listing lags the pushed items, which is why the iterator reads past the reported item count, so it must
        // catch up to the items the iterator read.
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

    /// <summary>
    /// A metamorph needs the run's own API token, so the test Actor stores its <c>APIFY_TOKEN</c> in its key-value store and
    /// then waits long enough for the test to read it.
    /// </summary>
    private static object MetamorphSourceActor(string name) => new
    {
        name,
        isPublic = false,
        description = "Integration test actor",
        versions = new[]
        {
            new
            {
                versionNumber = "0.0",
                sourceType = "SOURCE_FILES",
                buildTag = "latest",
                sourceFiles = new object[]
                {
                    new { name = "Dockerfile", format = "TEXT", content = "FROM apify/actor-node:22\nCOPY . ./\nCMD [\"node\", \"main.js\"]\n" },
                    new
                    {
                        name = "main.js",
                        format = "TEXT",
                        content = "const { Actor } = require('apify');\n"
                            + "Actor.main(async () => {\n"
                            + "    await Actor.setValue('TOKEN', process.env.APIFY_TOKEN, { contentType: 'text/plain' });\n"
                            + "    await new Promise((resolve) => setTimeout(resolve, 240000));\n"
                            + "});\n",
                    },
                },
            },
        },
    };

    [SkippableFact]
    public async Task MetamorphRunWithItsOwnToken()
    {
        var client = RequireClient();
        var created = await client.Actors().CreateAsync(MetamorphSourceActor(UniqueName("metamorph")));
        string? runId = null;
        try
        {
            var build = await client.Actor(created.Id!).BuildAsync("0.0");
            var finishedBuild = await client.Build(build.Id!).WaitForFinishAsync(300);
            Assert.Equal("SUCCEEDED", finishedBuild.Status);

            var run = await client.Actor(created.Id!).StartAsync(null, new ActorStartOptions { MemoryMbytes = 256, TimeoutSecs = 300 });
            runId = run.Id;

            // Poll the Actor's key-value store for the token it stores at startup, and fail early if the run ended without it.
            string? runToken = null;
            Assert.True(
                await PollUntilAsync(
                    60,
                    TimeSpan.FromSeconds(2),
                    async () =>
                    {
                        var record = await client.KeyValueStore(run.DefaultKeyValueStoreId!).GetRecordAsync("TOKEN");
                        if (record is not null)
                        {
                            runToken = Encoding.UTF8.GetString(record.Value);
                            return true;
                        }

                        var current = await client.Run(runId!).GetAsync();
                        Assert.Contains(current?.Status, new[] { "READY", "RUNNING" });
                        return false;
                    }),
                "expected the Actor to store its run token");

            // Only the run itself can metamorph, so use a client with the run's token.
            var runClient = RequireClient(options => options.Token = runToken);
            var metamorphed = await runClient.Run(runId!).MetamorphAsync("apify/hello-world", new { message = "Hello from metamorph!" });
            Assert.Equal(runId, metamorphed.Id);

            // Hello-world replaces the waiting Actor, so the run finishes on its own.
            var finalRun = await client.Run(runId!).WaitForFinishAsync(300);
            Assert.Equal("SUCCEEDED", finalRun.Status);
            var helloWorldId = await ResolveHelloWorldActorIdAsync(client);
            var metamorphs = finalRun.ToJsonObject()["metamorphs"] as JsonArray;
            Assert.NotNull(metamorphs);
            Assert.Contains(metamorphs!, m => m?["actorId"]?.GetValue<string>() == helloWorldId);
        }
        finally
        {
            if (runId is not null)
            {
                // Aborting a finished run returns it unchanged.
                await client.Run(runId).AbortAsync();
                await client.Run(runId).WaitForFinishAsync(300);
                await client.Run(runId).DeleteAsync();
            }

            await client.Actor(created.Id!).DeleteAsync();
        }
    }
}
