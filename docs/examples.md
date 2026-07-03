# Examples

Each example below is a complete, runnable scenario. The canonical, compiled versions live in
[`tests/Apify.Client.Tests/Examples`](../tests/Apify.Client.Tests/Examples) and are executed
end-to-end against the live API by the **Test examples** CI step (they require an `APIFY_TOKEN`), so
the snippets here are guaranteed to stay valid and working.

Every snippet runs inside an `async` context and assumes the following `using` directives appear at the
top of the file, **before** any top-level statements (a `using` after the first statement is a `CS1529`
compile error). `ImplicitUsings` is disabled in this repository, so even `System` is listed explicitly:

```csharp
using System;
using System.IO;
using System.Text;
using Apify.Client;
using Apify.Client.Models;
using Apify.Client.Options;

var client = new ApifyClient(Environment.GetEnvironmentVariable("APIFY_TOKEN"));
```

## Run a store Actor and read its dataset

```csharp
var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);
var items = await client.Dataset(run.DefaultDatasetId!).ListItemsAsync(new DatasetListItemsOptions());
Console.WriteLine("Item count: " + items.Count);
```

## Each storage: create, push, read

```csharp
// Dataset
var dataset = await client.Datasets().GetOrCreateAsync("example-ds");
await client.Dataset(dataset.Id!).PushItemsAsync(new[] { new { hello = "world" } });
var items = await client.Dataset(dataset.Id!).ListItemsAsync(new DatasetListItemsOptions());
Console.WriteLine("Dataset items: " + items.Count);

// Key-value store
var store = await client.KeyValueStores().GetOrCreateAsync("example-kvs");
await client.KeyValueStore(store.Id!).SetRecordJsonAsync("OUTPUT", new { answer = 42 });
var record = await client.KeyValueStore(store.Id!).GetRecordAsync("OUTPUT");
// GetRecordAsync returns the raw bytes; decode JSON/text records with UTF-8.
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
    await client.Dataset(last.DefaultDatasetId!).ListItemsAsync(new DatasetListItemsOptions());
    await client.KeyValueStore(last.DefaultKeyValueStoreId!).GetRecordAsync("OUTPUT");
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
var run = await client.Actor("apify/hello-world").StartAsync();
await client.Run(run.Id!).WaitForFinishAsync(120);
using var stream = await client.Run(run.Id!).GetStreamedLogAsync();
using var reader = new StreamReader(stream);
Console.WriteLine(await reader.ReadToEndAsync());
```
