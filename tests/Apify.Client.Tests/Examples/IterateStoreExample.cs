using System;
using System.Threading.Tasks;
using Apify.Client;
using Apify.Client.Options;

namespace Apify.Client.Tests.Examples;

/// <summary>Lazy iteration of Store Actors using the convenience iterator.</summary>
public static class IterateStoreExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        var shown = 0;
        await foreach (var item in client.Store().IterateAsync(new StoreListOptions { Limit = 10 }))
        {
            Console.WriteLine(item.Name);
            if (++shown >= 5)
            {
                break;
            }
        }
    }
}
