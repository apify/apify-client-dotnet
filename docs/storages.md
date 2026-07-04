# Storages

The three storage types — datasets, key-value stores and request queues — share the same collection
shape: `ListAsync(StorageListOptions?)` (one page), `IterateAsync(StorageListOptions?)` →
`IAsyncEnumerable<T>` (lazy, all pages), and `GetOrCreateAsync(name?)`. Storages can also be reached
from a run (`client.Run(id).Dataset()`, `.KeyValueStore()`, `.RequestQueue()`).

> Snippets below run inside an `async` context. `ImplicitUsings` is disabled in this repository, so all
> `using` directives (including `System`) are shown explicitly and must precede any statements.

`StorageListOptions`: `Offset`, `Limit`, `Desc`, `Unnamed`, `Ownership`.

## Datasets

`client.Datasets()` / `client.Dataset(id)`.

- `GetAsync()`, `UpdateAsync(newFields)`, `DeleteAsync()`.
- `ListItemsAsync(DatasetListItemsOptions? = null)` → `PaginationList<JsonNode?>` (one page; pagination via
  response headers).
- `IterateItemsAsync(DatasetListItemsOptions? = null)` → `IAsyncEnumerable<JsonNode?>` — lazily iterate every
  item across pages, fetching each page on demand.
- `DownloadItemsAsync(DownloadItemsFormat, DatasetDownloadOptions? = null)` → serialized items as `byte[]`
  (raw bytes, so binary formats like `Xlsx` are not corrupted; decode text formats yourself).
- `PushItemsAsync(object items)` — push one object or an array of objects.
- `GetStatisticsAsync()` → `JsonObject?`.
- `CreateItemsPublicUrlAsync(DatasetListItemsOptions?, int? expiresInSecs = null)` → signed public URL.

```csharp
using System;
using System.Text;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var dataset = await client.Datasets().GetOrCreateAsync("my-dataset");
await client.Dataset(dataset.Id!).PushItemsAsync(new[] { new { url = "https://a.com", n = 1 } });
var page = await client.Dataset(dataset.Id!).ListItemsAsync(new DatasetListItemsOptions { Limit = 100 });
Console.WriteLine(page.Count); // items in this page; page.Total is the count across all pages
var csvBytes = await client.Dataset(dataset.Id!).DownloadItemsAsync(DownloadItemsFormat.Csv);
Console.WriteLine(Encoding.UTF8.GetString(csvBytes)); // CSV is text; decode the raw bytes
```

## Key-value stores

`client.KeyValueStores()` / `client.KeyValueStore(id)`.

- `GetAsync()`, `UpdateAsync(newFields)`, `DeleteAsync()`.
- `ListKeysAsync(ListKeysOptions?)` → `KeyValueStoreKeysPage`.
- `RecordExistsAsync(key)` → `bool`.
- `GetRecordAsync(key, GetRecordOptions? = null)` → `KeyValueStoreRecord?`. `KeyValueStoreRecord.Value` is a
  `byte[]` of the record's raw bytes (so binary records survive intact); decode it according to
  `KeyValueStoreRecord.ContentType` — e.g. `Encoding.UTF8.GetString(record.Value)` for text, or
  `JsonSerializer.Deserialize<T>(record.Value)` for JSON.
- `SetRecordAsync(key, byte[] value, contentType, SetRecordOptions?)` and `SetRecordJsonAsync(key, value)`
  (serializes `value` to JSON bytes).
- `DeleteRecordAsync(key)`.
- `GetRecordPublicUrlAsync(key)` and `CreateKeysPublicUrlAsync(ListKeysOptions?, int? expiresInSecs)`.

```csharp
using System;
using System.Text;
using System.Text.Json;
using Apify.Client;

var client = new ApifyClient("my-api-token");
var store = await client.KeyValueStores().GetOrCreateAsync("my-store");

// Write JSON, then read it back and decode the raw bytes.
await client.KeyValueStore(store.Id!).SetRecordJsonAsync("OUTPUT", new { answer = 42 });
var record = await client.KeyValueStore(store.Id!).GetRecordAsync("OUTPUT");
if (record is not null)
{
    Console.WriteLine("content type: " + record.ContentType);
    Console.WriteLine("as text: " + Encoding.UTF8.GetString(record.Value));
    var output = JsonSerializer.Deserialize<JsonElement>(record.Value);
    Console.WriteLine("answer: " + output.GetProperty("answer").GetInt32());
}

// Write raw bytes directly (binary-safe).
await client.KeyValueStore(store.Id!).SetRecordAsync("blob", new byte[] { 0x00, 0xFF }, "application/octet-stream");
```

## Request queues

`client.RequestQueues()` / `client.RequestQueue(id, RequestQueueClientOptions?)`. The options set a
stable `ClientKey` (required to manage locks the client created) and a per-queue `TimeoutSecs`.

- `GetAsync()`, `UpdateAsync(newFields)`, `DeleteAsync()`.
- `AddRequestAsync(RequestQueueRequest, bool forefront = false)` → `RequestQueueOperationInfo`.
- `GetRequestAsync(id)`, `UpdateRequestAsync(request, forefront)`, `DeleteRequestAsync(id)`.
- `ListHeadAsync(int? limit)` → `RequestQueueHead`; `ListAndLockHeadAsync(lockSecs, limit?)`.
- `BatchAddRequestsAsync(IReadOnlyList<RequestQueueRequest>, forefront, BatchAddRequestsOptions?)` —
  auto-chunks by count (25) and payload size (~9 MiB) and retries unprocessed requests. Every request
  needs a non-empty `UniqueKey`.
- `ListRequestsAsync(ListRequestsOptions?)` and `PaginateRequestsAsync(PaginateRequestsOptions?)`
  (`IAsyncEnumerable`).
- Lock management: `ProlongRequestLockAsync`, `DeleteRequestLockAsync`, `UnlockRequestsAsync`.

```csharp
using System;
using System.Collections.Generic;
using Apify.Client;
using Apify.Client.Models;

var client = new ApifyClient("my-api-token");
var queue = await client.RequestQueues().GetOrCreateAsync("my-queue");
var rq = client.RequestQueue(queue.Id!);

await rq.AddRequestAsync(new RequestQueueRequest("https://example.com", "example"));

var batch = new List<RequestQueueRequest>();
for (var i = 0; i < 100; i++)
{
    batch.Add(new RequestQueueRequest($"https://example.com/{i}", $"key-{i}"));
}
var result = await rq.BatchAddRequestsAsync(batch);
Console.WriteLine(result.ProcessedRequests.Count);

await foreach (var request in rq.PaginateRequestsAsync())
{
    Console.WriteLine(request.Url);
}
```
