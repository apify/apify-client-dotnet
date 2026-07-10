using System;
using System.Threading.Tasks;
using Apify.Client;
using Apify.Client.Options;

namespace Apify.Client.Tests.Examples;

/// <summary>Run a store Actor and read its default dataset.</summary>
public static class RunStoreActorExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);
        var items = await client.Dataset(run.DefaultDatasetId!).ListItemsAsync(new DatasetListItemsOptions());
        // Count is the number of items in THIS page; Total is the dataset's full count across all pages.
        Console.WriteLine($"Items on this page: {items.Count} (of {items.Total} total)");
    }
}
