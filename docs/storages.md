# Storages

The three storage types — datasets, key-value stores and request queues — share the same collection
shape: `ListAsync(StorageListOptions? options = null)` (one page),
`IterateAsync(StorageListOptions? options = null)` → `IAsyncEnumerable<T>` (lazy, all pages), and
`GetOrCreateAsync(string? name = null)` (dataset and key-value store collections additionally accept an
optional `JsonNode? schema = null` to register a storage schema on creation). Storages can also be
reached from a run (`client.Run(id).Dataset()`, `.KeyValueStore()`, `.RequestQueue()`).

> Snippets below run inside an `async` context. `ImplicitUsings` is disabled in this repository, so all
> `using` directives (including `System`) are shown explicitly and must precede any statements.

`StorageListOptions` fields:

| Field | Type | Description |
|---|---|---|
| `Offset` | `int?` | Number of storages to skip from the start. |
| `Limit` | `int?` | Maximum number of storages to return in the page. |
| `Desc` | `bool?` | Sort newest-first when `true`. |
| `Unnamed` | `bool?` | Include unnamed storages when `true` (they are excluded by default). |
| `Ownership` | `string?` | Filter by ownership (e.g. only storages owned by the current account). |

## Datasets

`client.Datasets()` / `client.Dataset(id)`.

- `GetAsync()` → `Dataset?`; `UpdateAsync(object newFields)` → `Dataset`; `DeleteAsync()`.
- `ListItemsAsync(DatasetListItemsOptions? options = null)` → `PaginationList<JsonNode?>` (one page; pagination via
  response headers).
- `IterateItemsAsync(DatasetListItemsOptions? options = null)` → `IAsyncEnumerable<JsonNode?>` — lazily iterate every
  item across pages, fetching each page on demand.
- `DownloadItemsAsync(DownloadItemsFormat format, DatasetDownloadOptions? options = null)` → serialized items as
  `byte[]` (raw bytes, so binary formats like `Xlsx` are not corrupted; decode text formats yourself).
- `PushItemsAsync(object items)` — push one object or an array of objects.
- `GetStatisticsAsync()` → `JsonObject?`.
- `CreateItemsPublicUrlAsync(DatasetListItemsOptions? options = null, int? expiresInSecs = null)` → `string` (a signed public URL).

`DatasetListItemsOptions` selects and reshapes items: `Offset`/`Limit` (pagination), `Desc` (reverse
order), `Fields`/`OutputFields`/`Omit` (choose columns), `Unwind`/`Flatten` (restructure nested
fields), `Clean`/`SkipEmpty`/`SkipHidden`/`SkipFailedPages` (drop unwanted rows), `Simplified`,
`View`, and `Signature` (for signed public URLs).

`DownloadItemsAsync`'s `format` argument is the `DownloadItemsFormat` enum, whose values map to the
API's export formats:

| Value | Format |
|---|---|
| `Json` | JSON array. |
| `Jsonl` | Newline-delimited JSON. |
| `Csv` | Comma-separated values. |
| `Xlsx` | Microsoft Excel (XLSX) workbook (binary). |
| `Xml` | XML. |
| `Rss` | RSS feed. |
| `Html` | HTML table. |

`DatasetDownloadOptions` adds format-specific export options on top of the item filtering/projection:

| Field | Type | Description |
|---|---|---|
| `Items` | `DatasetListItemsOptions?` | The shared item filtering/projection options to apply before export. |
| `Attachment` | `bool?` | Set `Content-Disposition: attachment` on the response. |
| `Bom` | `bool?` | Prepend a UTF-8 BOM (useful for Excel-compatible CSV). |
| `Delimiter` | `string?` | The CSV field delimiter (default `,`). |
| `SkipHeaderRow` | `bool?` | Omit the CSV header row. |
| `XmlRoot` | `string?` | Name of the root XML element (default `items`). |
| `XmlRow` | `string?` | Name of the per-item XML element (default `item`). |
| `FeedTitle` | `string?` | Title used for RSS/Atom feed exports. |
| `FeedDescription` | `string?` | Description used for RSS/Atom feed exports. |

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

- `GetAsync()` → `KeyValueStore?`; `UpdateAsync(object newFields)` → `KeyValueStore`; `DeleteAsync()`.
- `ListKeysAsync(ListKeysOptions? options = null)` → `KeyValueStoreKeysPage`.
- `RecordExistsAsync(string key)` → `bool`.
- `GetRecordAsync(string key, GetRecordOptions? options = null)` → `KeyValueStoreRecord?`.
  `KeyValueStoreRecord.Value` is a `byte[]` of the record's raw bytes (so binary records survive intact);
  decode it according to `KeyValueStoreRecord.ContentType` — e.g. `Encoding.UTF8.GetString(record.Value)`
  for text, or `JsonSerializer.Deserialize<T>(record.Value)` for JSON.
- `SetRecordAsync(string key, byte[] value, string contentType, SetRecordOptions? options = null)` and
  `SetRecordJsonAsync(string key, object? value)` (serializes `value` to JSON bytes).
- `DeleteRecordAsync(string key)`.
- `GetRecordPublicUrlAsync(string key)` → `string` and
  `CreateKeysPublicUrlAsync(ListKeysOptions? options = null, int? expiresInSecs = null)` → `string` (both signed public URLs).

`ListKeysOptions` fields: `Limit` (page size), `ExclusiveStartKey` (start after this key),
`Prefix` (only keys with this prefix), `Collection` (a named record collection), and `Signature`
(for signed public URLs). `GetRecordOptions` fields: `Attachment` (request a download disposition)
and `Signature`. `SetRecordOptions` fields: `TimeoutSecs` and `DoNotRetryTimeouts`.

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

`client.RequestQueues()` / `client.RequestQueue(id, RequestQueueClientOptions? options = null)`. The
options set a stable `ClientKey` (required to manage locks the client created) and a per-queue
`TimeoutSecs`.

- `GetAsync()` → `RequestQueue?`; `UpdateAsync(object newFields)` → `RequestQueue`; `DeleteAsync()`.
- `AddRequestAsync(RequestQueueRequest request, bool forefront = false)` → `RequestQueueOperationInfo`.
- `GetRequestAsync(string id)` → `RequestQueueRequest?` (null if not found);
  `UpdateRequestAsync(RequestQueueRequest request, bool forefront = false)` → `RequestQueueOperationInfo`;
  `DeleteRequestAsync(string id)` (no return value).
- `ListHeadAsync(int? limit = null)` → `RequestQueueHead`;
  `ListAndLockHeadAsync(int lockSecs, int? limit = null)` → `LockedRequestQueueHead` (each item's
  `RequestQueueRequest.LockExpiresAt`/`RetryCount` is populated).
- `BatchAddRequestsAsync(IReadOnlyList<RequestQueueRequest> requests, bool forefront = false, BatchAddRequestsOptions? options = null)`
  → `BatchAddResult` — auto-chunks by count (25) and payload size (~9 MiB) and retries unprocessed
  requests. Every request needs a non-empty `UniqueKey`.
- `BatchDeleteRequestsAsync(IReadOnlyList<RequestQueueRequest> requests)` → `BatchDeleteResult` — delete a
  batch of requests in one call; each entry identifies the request to delete via `Id` and/or `UniqueKey`.
- `ListRequestsAsync(ListRequestsOptions? options = null)` → `RequestQueueRequestsPage` and
  `PaginateRequestsAsync(PaginateRequestsOptions? options = null)` → `IAsyncEnumerable<RequestQueueRequest>`.
- Lock management: `ProlongRequestLockAsync(string id, int lockSecs, bool forefront = false)` →
  `RequestLockInfo`, `DeleteRequestLockAsync(string id, bool forefront = false)`,
  `UnlockRequestsAsync()` → `UnlockRequestsResult`.
- `WithClientKey(string clientKey)` → `RequestQueueClient` — returns a copy of this client bound to the
  given client key (a fluent alternative to passing `RequestQueueClientOptions.ClientKey` on
  `client.RequestQueue(id, options)`); the client key ties lock ownership to this client.

`BatchAddRequestsOptions` fields: `MaxUnprocessedRequestsRetries` (retry attempts for requests the API
leaves unprocessed), `MaxParallel` (how many chunks are sent concurrently), and
`MinDelayBetweenUnprocessedRequestsRetriesMillis` (backoff before retrying unprocessed requests).
`ListRequestsOptions`/`PaginateRequestsOptions` fields: `Limit`, `ExclusiveStartId`, `Cursor`, and
`Filter` (an `IReadOnlyList<string>?` — one or more of `"pending"`/`"locked"`, so several states can be
requested at once); `PaginateRequestsOptions` also has `MaxPageLimit`.

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
