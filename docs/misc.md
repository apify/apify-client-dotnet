# Store, users and logs

> Snippets below run inside an `async` context. `ImplicitUsings` is disabled in this repository, so all
> `using` directives (including `System`) are shown explicitly and must precede any statements.

## Apify Store — `client.Store()`

Browse public Actors in the [Apify Store](https://apify.com/store).

- `ListAsync(StoreListOptions?)` → `PaginationList<ActorStoreListItem>` (one page).
- `IterateAsync(StoreListOptions?)` → `IAsyncEnumerable<ActorStoreListItem>` (lazy, all pages;
  `Limit` is the page size).

`StoreListOptions`: `Offset`, `Limit`, `Search`, `SortBy`, `Category`, `Username`, `PricingModel`,
`IncludeUnrunnableActors`, `AllowsAgenticUsers`, `ResponseFormat`.

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
  (`ToJsonObject()`).
- `MonthlyUsageAsync(string? date = null)` → `JsonObject` (only for `Me()`).
- `LimitsAsync()` / `UpdateLimitsAsync(object newLimits)` (only for `Me()`).

```csharp
using System;
using Apify.Client;

var client = new ApifyClient("my-api-token");
var me = await client.Me().GetAsync();
Console.WriteLine(me?.Username);
var usage = await client.Me().MonthlyUsageAsync();
```

## Logs — `client.Log(buildOrRunId)`

- `GetAsync(LogOptions?)` → `string?` (buffered).
- `StreamAsync(LogOptions?)` → `Stream` (live). Also `client.Run(id).GetStreamedLogAsync()`.

`LogOptions`: `Raw`, `Download`.

```csharp
using System;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var log = await client.Log("some-run-id").GetAsync(new LogOptions { Raw = true });
Console.WriteLine(log);
```
