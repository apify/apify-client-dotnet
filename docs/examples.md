# Examples

Each example is a complete scenario. Compiled versions live in the repository under
[`tests/Apify.Client.Tests/Examples`](https://github.com/apify/apify-client-dotnet/tree/master/tests/Apify.Client.Tests/Examples)
and run against the live API in the **Test examples** CI step, so they stay working. They need an `APIFY_TOKEN`.

Every snippet runs in an `async` context after these directives, which must come before any top-level statement.
`System` is listed because `ImplicitUsings` is disabled:

```csharp
using System;
using System.Text;
using Apify.Client;
using Apify.Client.Models;
using Apify.Client.Options;

var client = new ApifyClient(
    Environment.GetEnvironmentVariable("APIFY_TOKEN")
    ?? throw new InvalidOperationException("Set the APIFY_TOKEN environment variable."));
```

## Run a store Actor and read its dataset

```csharp
// The third argument bounds the wait in seconds; null waits indefinitely.
var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);
var items = await client.Dataset(run.DefaultDatasetId!).ListItemsAsync(new DatasetListItemsOptions());
// Items.Count is the number of items on this page; Total is the count across all pages.
Console.WriteLine($"Items on this page: {items.Items.Count} (of {items.Total} total)");
```

## Each storage: create, push, read

```csharp
// Dataset
var dataset = await client.Datasets().GetOrCreateAsync("example-ds");
await client.Dataset(dataset.Id!).PushItemsAsync(new[] { new { hello = "world" } });
var items = await client.Dataset(dataset.Id!).ListItemsAsync(new DatasetListItemsOptions());
Console.WriteLine("Dataset items: " + items.Items.Count);

// Key-value store
var store = await client.KeyValueStores().GetOrCreateAsync("example-kvs");
await client.KeyValueStore(store.Id!).SetRecordJsonAsync("OUTPUT", new { answer = 42 });
var record = await client.KeyValueStore(store.Id!).GetRecordAsync("OUTPUT");
// GetRecordAsync returns a record whose .Value is the raw bytes; decode JSON/text with UTF-8.
var recordText = record is null ? string.Empty : Encoding.UTF8.GetString(record.Value);
Console.WriteLine("KVS record: " + recordText);

// Request queue
var queue = await client.RequestQueues().GetOrCreateAsync("example-rq");
await client.RequestQueue(queue.Id!).AddRequestAsync(new RequestQueueRequest("https://example.com", "example"));
var head = await client.RequestQueue(queue.Id!).ListHeadAsync(10);
Console.WriteLine("Queue head size: " + head.Items.Count);
```

## Get own account details

```csharp
var user = await client.Me().GetAsync();
if (user is not null)
{
    Console.WriteLine("Account " + user.Id + " / " + user.Username);
}
```

## Create an Actor, build it, run it, print the log

```csharp
var created = await client.Actors().CreateAsync(new
{
    name = "example-actor",
    isPublic = false,
    versions = new[]
    {
        new
        {
            versionNumber = "0.0",
            sourceType = "SOURCE_FILES",
            buildTag = "latest",
            sourceFiles = new object[]
            {
                new { name = "Dockerfile", format = "TEXT", content = "FROM apify/actor-node:20\nCOPY . ./\nCMD node main.js" },
                new { name = "main.js", format = "TEXT", content = "console.log('hi');" },
            },
        },
    },
});

var build = await client.Actor(created.Id!).BuildAsync("0.0", new ActorBuildOptions());
await client.Build(build.Id!).WaitForFinishAsync(300);
var run = await client.Actor(created.Id!).CallAsync(null, null, 120);
var log = await client.Run(run.Id!).Log().GetAsync();
Console.WriteLine(log);
```

## Start a run, then read the last run's storages

```csharp
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
```

## Lazy iteration of the Apify Store

```csharp
var shown = 0;
await foreach (var item in client.Store().IterateAsync(new StoreListOptions { Limit = 10 }))
{
    Console.WriteLine(item.Name);
    if (++shown >= 5)
    {
        break;
    }
}
```

## Run an Actor with log redirection (streaming)

```csharp
// The `log` argument redirects the run's live log to the given sink (here stdout) while it runs; the
// client streams the log and forwards each complete message as it arrives.
await client.Actor("apify/hello-world").CallAsync(null, null, 120, log: Console.WriteLine);
```

You can also redirect a specific run's log yourself with `GetStreamedLog`:

```csharp
var run = await client.Actor("apify/hello-world").StartAsync();
await using var streamedLog = client.Run(run.Id!).GetStreamedLog(Console.WriteLine);
streamedLog.Start();
await client.Run(run.Id!).WaitForFinishAsync(120);
await streamedLog.StopAsync();
```
