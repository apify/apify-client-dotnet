using System;
using System.Threading.Tasks;
using Apify.Client;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Tests.Examples;

/// <summary>Start a run, wait, then fetch the Actor's last run and its storages.</summary>
public static class RunAndLastRunStoragesExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        await client.Actor("apify/hello-world").CallAsync(null, null, 120);

        // Resolve the last run, filtering by both status and how it was started (origin).
        var last = await client.Actor("apify/hello-world")
            .LastRun(new LastRunOptions { Status = ActorJobStatus.Succeeded, Origin = RunOrigin.Api })
            .GetAsync();
        if (last is not null)
        {
            await client.Dataset(last.DefaultDatasetId!).ListItemsAsync(new DatasetListItemsOptions());
            await client.KeyValueStore(last.DefaultKeyValueStoreId!).GetRecordAsync("OUTPUT");
            Console.WriteLine("Last run: " + last.Id);
        }

        // List the Actor's finished-or-running runs by passing several statuses at once.
        var runs = await client.Actor("apify/hello-world").Runs().ListAsync(
            new ListOptions { Limit = 5 },
            new RunListOptions { Status = new[] { ActorJobStatus.Succeeded, ActorJobStatus.Running } });
        Console.WriteLine("Matching runs on this page: " + runs.Count);
    }
}
