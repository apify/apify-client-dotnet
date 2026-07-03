using System;
using System.Threading.Tasks;
using Apify.Client;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Tests.Examples;

/// <summary>Each storage: create, push data, read data back.</summary>
public static class StoragesExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        // Dataset
        var dataset = await client.Datasets().GetOrCreateAsync("dotnet-example-ds-" + Suffix());
        try
        {
            await client.Dataset(dataset.Id!).PushItemsAsync(new[] { new { hello = "world" } });
            var items = await client.Dataset(dataset.Id!).ListItemsAsync(new DatasetListItemsOptions());
            Console.WriteLine("Dataset items: " + items.Count);
        }
        finally
        {
            await client.Dataset(dataset.Id!).DeleteAsync();
        }

        // Key-value store
        var store = await client.KeyValueStores().GetOrCreateAsync("dotnet-example-kvs-" + Suffix());
        try
        {
            await client.KeyValueStore(store.Id!).SetRecordJsonAsync("OUTPUT", new { answer = 42 });
            var record = await client.KeyValueStore(store.Id!).GetRecordAsync("OUTPUT");
            // Value is the raw bytes; decode JSON/text records via the reported content type.
            var recordText = record is null ? string.Empty : System.Text.Encoding.UTF8.GetString(record.Value);
            Console.WriteLine("KVS record: " + recordText);
        }
        finally
        {
            await client.KeyValueStore(store.Id!).DeleteAsync();
        }

        // Request queue
        var queue = await client.RequestQueues().GetOrCreateAsync("dotnet-example-rq-" + Suffix());
        try
        {
            await client.RequestQueue(queue.Id!).AddRequestAsync(new RequestQueueRequest("https://example.com", "example"));
            var head = await client.RequestQueue(queue.Id!).ListHeadAsync(10);
            Console.WriteLine("Queue head size: " + head.Items.Count);
        }
        finally
        {
            await client.RequestQueue(queue.Id!).DeleteAsync();
        }
    }

    private static string Suffix()
        => Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
}
