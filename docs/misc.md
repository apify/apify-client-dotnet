# Store, users and logs

> Snippets below run inside an `async` context. `ImplicitUsings` is disabled in this repository, so all
> `using` directives (including `System`) are shown explicitly and must precede any statements.

## Apify Store — `client.Store()`

Browse public Actors in the [Apify Store](https://apify.com/store).

- `ListAsync(StoreListOptions? options = null)` → `PaginationList<ActorStoreListItem>` (one page).
- `IterateAsync(StoreListOptions? options = null)` → `IAsyncEnumerable<ActorStoreListItem>` (lazy, all
  pages; `Limit` caps the total number of Actors yielded and `ChunkSize` sets the page size).

`StoreListOptions` fields:

| Field | Type | Description |
|---|---|---|
| `Offset` | `int?` | Number of Actors to skip from the start. |
| `Limit` | `int?` | Maximum number of Actors to return (for `IterateAsync`, in total across all pages). |
| `ChunkSize` | `int?` | Page size used by `IterateAsync` (positive; the smaller of this and the remaining `Limit` is requested per call). Never sent to the API. |
| `Search` | `string?` | Full-text search string to filter Actors by. |
| `SortBy` | `string?` | Field to sort by (e.g. `popularity`, `newest`). |
| `Category` | `string?` | Restrict results to a Store category. |
| `Username` | `string?` | Restrict results to a given owner's Actors. |
| `PricingModel` | `string?` | Filter by pricing model (`FREE`, `PRICE_PER_DATASET_ITEM`, `PAY_PER_EVENT`; the rental model `FLAT_PRICE_PER_MONTH` is no longer supported). |
| `IncludeUnrunnableActors` | `bool?` | Include Actors that cannot currently be run. |
| `AllowsAgenticUsers` | `bool?` | Only Actors that permit agentic (automated) users. |
| `IncludeInputSchema` | `bool?` | Include each Actor's compact `inputSchema` (read it with `Get("inputSchema")`). The API then caps `limit` at 10, so iteration requests pages of at most 10. |
| `ResponseFormat` | `string?` | Requested response format. |

```csharp
using System;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
await foreach (var item in client.Store().IterateAsync(new StoreListOptions { Search = "crawler", Limit = 50 }))
{
    Console.WriteLine(item.Name);
}
```

## Users — `client.Me()` / `client.User(id)`

- `GetAsync()` → `User?`. For `Me()` the raw payload includes private account details
  (`ToJsonObject()`); for `User(id)` it returns the public profile.
- `MonthlyUsageAsync(string? date = null)` → `JsonObject` (`date` is `YYYY-MM-DD`, and `null` reports the
  current month).
- `LimitsAsync()` → `JsonObject` / `UpdateLimitsAsync(object newLimits)`.

The usage and limits methods work on `Me()` and on `User(id)` with your own user ID; the API rejects another
user's.

```csharp
using System;
using Apify.Client;

var client = new ApifyClient("my-api-token");
var me = await client.Me().GetAsync();
Console.WriteLine(me?.Username);
var usage = await client.Me().MonthlyUsageAsync();

// Any user's public profile by id (no private fields).
var otherUser = await client.User("some-user-id").GetAsync();
Console.WriteLine(otherUser?.Username);
```

## Logs — `client.Log(buildOrRunId)`

- `GetAsync(LogOptions? options = null)` → `string?` (buffered; `null` if the log does not exist).
- `GetBytesAsync(LogOptions? options = null)` → `byte[]?` — the same buffered fetch as `GetAsync`, as raw
  bytes instead of a decoded string (for a log that is not necessarily valid UTF-8 text).
- `StreamAsync(LogOptions? options = null)` → `Stream?` (live; `null` if the log does not exist). Also
  `client.Run(id).GetStreamedLogAsync()`.

Addressed by its own id (`client.Log(buildOrRunId)`) as above, a missing log resolves to `null`. Reached
through a run or build instead (`client.Run(id).Log()`, `client.Build(id).Log()`), a 404 is ambiguous —
it could mean the run/build itself or its log is missing — so `GetAsync`/`GetBytesAsync` throw an
`ApifyApiException` there instead.

`LogOptions` fields: `Raw` (`bool?`, return the unprocessed log rather than the parsed form) and
`Download` (`bool?`, request a download `Content-Disposition`).

```csharp
using System;
using System.IO;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var log = await client.Log("some-run-id").GetAsync(new LogOptions { Raw = true });
Console.WriteLine(log);

// Read a run's raw live log as a stream (the low-level alternative to GetStreamedLog's sink redirection).
await using var logStream = await client.Run("some-run-id").GetStreamedLogAsync();
using var reader = new StreamReader(logStream);
Console.WriteLine(await reader.ReadToEndAsync());
```
