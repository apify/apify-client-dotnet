# Store, users and logs

> Snippets below run inside an `async` context. `ImplicitUsings` is disabled in this repository, so all
> `using` directives (including `System`) are shown explicitly and must precede any statements.

## Apify Store — `client.Store()`

Browse public Actors in the [Apify Store](https://apify.com/store).

- `ListAsync(StoreListOptions? options = null)` → `PaginationList<ActorStoreListItem>` (one page).
- `IterateAsync(StoreListOptions? options = null)` → `IAsyncEnumerable<ActorStoreListItem>` (lazy, all
  pages; `Limit` is the page size).

`StoreListOptions` fields:

| Field | Type | Description |
|---|---|---|
| `Offset` | `int?` | Number of Actors to skip from the start. |
| `Limit` | `int?` | Maximum number of Actors to return in the page. |
| `Search` | `string?` | Full-text search string to filter Actors by. |
| `SortBy` | `string?` | Field to sort by (e.g. `popularity`, `newest`). |
| `Category` | `string?` | Restrict results to a Store category. |
| `Username` | `string?` | Restrict results to a given owner's Actors. |
| `PricingModel` | `string?` | Filter by pricing model (`FREE`, `FLAT_PRICE_PER_MONTH`, `PRICE_PER_DATASET_ITEM`, …). |
| `IncludeUnrunnableActors` | `bool?` | Include Actors that cannot currently be run. |
| `AllowsAgenticUsers` | `bool?` | Only Actors that permit agentic (automated) users. |
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
- `MonthlyUsageAsync(string? date = null)` → `JsonObject` (only for `Me()`; `date` is `YYYY-MM-DD`, and
  `null` reports the current month).
- `LimitsAsync()` → `JsonObject` / `UpdateLimitsAsync(object newLimits)` (only for `Me()`).

```csharp
using System;
using Apify.Client;

var client = new ApifyClient("my-api-token");
var me = await client.Me().GetAsync();
Console.WriteLine(me?.Username);
var usage = await client.Me().MonthlyUsageAsync();
```

## Logs — `client.Log(buildOrRunId)`

- `GetAsync(LogOptions? options = null)` → `string?` (buffered).
- `StreamAsync(LogOptions? options = null)` → `Stream` (live). Also `client.Run(id).GetStreamedLogAsync()`.

`LogOptions` fields: `Raw` (`bool?`, return the unprocessed log rather than the parsed form) and
`Download` (`bool?`, request a download `Content-Disposition`).

```csharp
using System;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var log = await client.Log("some-run-id").GetAsync(new LogOptions { Raw = true });
Console.WriteLine(log);
```
