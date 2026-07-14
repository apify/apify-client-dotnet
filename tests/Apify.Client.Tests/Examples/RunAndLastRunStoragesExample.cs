using System;
using System.Threading.Tasks;
using Apify.Client;
using Apify.Client.Options;

namespace Apify.Client.Tests.Examples;

/// <summary>Start a run, wait, then fetch the Actor's last run and its storages.</summary>
public static class RunAndLastRunStoragesExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        await client.Actor("apify/hello-world").CallAsync(null, null, 120);
        var last = await client.Actor("apify/hello-world").LastRun(new LastRunOptions { Status = "SUCCEEDED" }).GetAsync();
        if (last is not null)
        {
            // Read all three of the run's default storages: dataset, key-value store, request queue.
            await client.Dataset(last.DefaultDatasetId!).ListItemsAsync(new DatasetListItemsOptions());
            await client.KeyValueStore(last.DefaultKeyValueStoreId!).GetRecordAsync("OUTPUT");
            await client.RequestQueue(last.DefaultRequestQueueId!).GetAsync();
            Console.WriteLine("Last run: " + last.Id);
        }
    }
}
