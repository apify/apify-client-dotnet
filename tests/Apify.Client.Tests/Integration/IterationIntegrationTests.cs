using System.Collections.Generic;
using System.Threading.Tasks;
using Apify.Client.Models;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

/// <summary>
/// Iteration coverage for the 12 <em>collection</em>-of-resources <c>IterateAsync</c> clients that had no
/// dedicated test before this suite was added (Actors, Actor versions, Actor environment variables,
/// datasets, key-value stores, request queues, tasks, schedules, webhooks, builds, runs, and webhook
/// dispatches — exercised by the 11 test methods below, since Actor versions and Actor environment
/// variables share one). <see cref="StoreIntegrationTests.IterateStore"/> already covers the thirteenth
/// (<c>StoreCollectionClient</c>), so it is not duplicated here.
/// </summary>
/// <remarks>
/// This suite is about iterating a collection of resources (e.g. "every dataset"), not a resource's own
/// contents: dataset item iteration (<c>DatasetClient.IterateItemsAsync</c>) is covered by a unit test
/// (<c>AutoPagingTests</c>) against a mocked transport, since it needs to exercise multi-page paging logic
/// deterministically; request-queue request iteration has its own integration test
/// (<c>RequestQueueIntegrationTests.RequestQueuePaginateMultiplePages</c>). Key-value stores expose no item
/// iterator (only <c>ListKeysAsync</c>), so there is nothing to cover there.
///
/// Where creation is cheap, each test below creates a couple of uniquely-named resources and asserts that
/// iterating the collection (newest-first) eventually surfaces every one of them — exercising the real
/// async generator, not just a single <c>ListAsync</c> call. Builds, runs, and webhook dispatches are
/// expensive or slow to create on demand, so those tests instead drain a <c>Limit</c>-bounded slice of
/// whatever already exists on the test account and assert the iterator behaves (terminates, respects the
/// cap, yields well-formed items).
/// </remarks>
[Trait("Category", "Integration")]
public sealed class IterationIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task IterateDatasets()
    {
        var client = RequireClient();
        var ids = new HashSet<string>();
        try
        {
            for (var i = 0; i < 3; i++)
            {
                ids.Add((await client.Datasets().GetOrCreateAsync(UniqueName("it-ds-" + i))).Id!);
            }

            Assert.True(
                await FindsAllEventuallyAsync(
                    () => client.Datasets().IterateAsync(new StorageListOptions { Desc = true }),
                    static d => d.Id!,
                    ids),
                "expected iteration to eventually find every created dataset");
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.Dataset(id).DeleteAsync();
            }
        }
    }

    [SkippableFact]
    public async Task IterateKeyValueStores()
    {
        var client = RequireClient();
        var ids = new HashSet<string>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                ids.Add((await client.KeyValueStores().GetOrCreateAsync(UniqueName("it-kvs-" + i))).Id!);
            }

            Assert.True(
                await FindsAllEventuallyAsync(
                    () => client.KeyValueStores().IterateAsync(new StorageListOptions { Desc = true }),
                    static s => s.Id!,
                    ids),
                "expected iteration to eventually find every created key-value store");
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.KeyValueStore(id).DeleteAsync();
            }
        }
    }

    [SkippableFact]
    public async Task IterateRequestQueues()
    {
        var client = RequireClient();
        var ids = new HashSet<string>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                ids.Add((await client.RequestQueues().GetOrCreateAsync(UniqueName("it-rq-" + i))).Id!);
            }

            Assert.True(
                await FindsAllEventuallyAsync(
                    () => client.RequestQueues().IterateAsync(new StorageListOptions { Desc = true }),
                    static q => q.Id!,
                    ids),
                "expected iteration to eventually find every created request queue");
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.RequestQueue(id).DeleteAsync();
            }
        }
    }

    [SkippableFact]
    public async Task IterateTasks()
    {
        var client = RequireClient();
        var ids = new HashSet<string>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                ids.Add((await client.Tasks().CreateAsync(MinimalTask(UniqueName("it-task-" + i)))).Id!);
            }

            Assert.True(
                await FindsAllEventuallyAsync(
                    () => client.Tasks().IterateAsync(new ListOptions { Desc = true }),
                    static t => t.Id!,
                    ids),
                "expected iteration to eventually find every created task");
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.Task(id).DeleteAsync();
            }
        }
    }

    [SkippableFact]
    public async Task IterateSchedules()
    {
        var client = RequireClient();
        var ids = new HashSet<string>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                ids.Add((await client.Schedules().CreateAsync(MinimalSchedule(UniqueName("it-sch-" + i)))).Id!);
            }

            Assert.True(
                await FindsAllEventuallyAsync(
                    () => client.Schedules().IterateAsync(new ListOptions { Desc = true }),
                    static s => s.Id!,
                    ids),
                "expected iteration to eventually find every created schedule");
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.Schedule(id).DeleteAsync();
            }
        }
    }

    [SkippableFact]
    public async Task IterateWebhooks()
    {
        var client = RequireClient();
        var ids = new HashSet<string>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                ids.Add((await client.Webhooks().CreateAsync(MinimalWebhook("https://example.com/it-wh-" + i))).Id!);
            }

            Assert.True(
                await FindsAllEventuallyAsync(
                    () => client.Webhooks().IterateAsync(new ListOptions { Desc = true }),
                    static w => w.Id!,
                    ids),
                "expected iteration to eventually find every created webhook");
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.Webhook(id).DeleteAsync();
            }
        }
    }

    [SkippableFact]
    public async Task IterateActors()
    {
        var client = RequireClient();
        var ids = new HashSet<string>();
        try
        {
            for (var i = 0; i < 2; i++)
            {
                ids.Add((await client.Actors().CreateAsync(MinimalActor(UniqueName("it-act-" + i)))).Id!);
            }

            // Restrict to the current user's Actors so iteration finds the freshly-created ones quickly
            // rather than scanning the public store.
            Assert.True(
                await FindsAllEventuallyAsync(
                    () => client.Actors().IterateAsync(new ActorListOptions { My = true, Desc = true }),
                    static a => a.Id!,
                    ids),
                "expected iteration to eventually find every created Actor");
        }
        finally
        {
            foreach (var id in ids)
            {
                await client.Actor(id).DeleteAsync();
            }
        }
    }

    [SkippableFact]
    public async Task IterateActorVersionsAndEnvVars()
    {
        var client = RequireClient();
        var actor = await client.Actors().CreateAsync(MinimalActor(UniqueName("it-ver")));
        try
        {
            var actorClient = client.Actor(actor.Id!);

            // The versions endpoint is not paginated (one fetch returns every version); draining the
            // iterator fully must terminate and must not re-yield a version. The minimal Actor ships with
            // version 0.0, so iteration yields at least that one version, exactly once.
            var versionNumbers = new HashSet<string>();
            var versionCount = 0;
            await foreach (var version in actorClient.Versions().IterateAsync())
            {
                versionNumbers.Add(version.VersionNumber!);
                versionCount++;
            }

            Assert.True(versionCount >= 1, "expected at least the initial version");
            Assert.Equal(versionCount, versionNumbers.Count);
            Assert.Contains("0.0", versionNumbers);

            var envVars = actorClient.Version("0.0").EnvVars();
            await envVars.CreateAsync(new ActorEnvVar("IT_VAR_A", "a"));
            await envVars.CreateAsync(new ActorEnvVar("IT_VAR_B", "b"));

            var seen = new HashSet<string>();
            await foreach (var envVar in envVars.IterateAsync())
            {
                seen.Add(envVar.Name!);
            }

            Assert.True(seen.Contains("IT_VAR_A") && seen.Contains("IT_VAR_B"), "saw " + string.Join(",", seen));
        }
        finally
        {
            await client.Actor(actor.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task IterateBuildsBounded()
    {
        var client = RequireClient();
        // Builds require building an Actor (expensive); assert a Limit-bounded slice of whatever already
        // exists iterates cleanly instead of creating a fresh build here.
        var count = 0;
        await foreach (var build in client.Builds().IterateAsync(new ListOptions { Limit = 5 }))
        {
            Assert.False(string.IsNullOrEmpty(build.Id));
            count++;
        }

        Assert.True(count <= 5, "the total-cap limit must bound iteration; got " + count);
    }

    [SkippableFact]
    public async Task IterateRunsBounded()
    {
        var client = RequireClient();
        var count = 0;
        await foreach (var run in client.Runs().IterateAsync(new ListOptions { Limit = 5 }, new RunListOptions()))
        {
            Assert.False(string.IsNullOrEmpty(run.Id));
            count++;
        }

        Assert.True(count <= 5, "the total-cap limit must bound iteration; got " + count);
    }

    [SkippableFact]
    public async Task IterateWebhookDispatchesBounded()
    {
        var client = RequireClient();
        var count = 0;
        await foreach (var dispatch in client.WebhookDispatches().IterateAsync(new ListOptions { Limit = 5 }))
        {
            Assert.False(string.IsNullOrEmpty(dispatch.Id));
            count++;
        }

        Assert.True(count <= 5, "the total-cap limit must bound iteration; got " + count);
    }
}
