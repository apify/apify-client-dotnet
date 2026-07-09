# Runs

Access the account-wide run collection with `client.Runs()`, an Actor's or task's runs with
`client.Actor(id).Runs()` / `client.Task(id).Runs()`, and a specific run with `client.Run(runId)`.

## Collection

- `ListAsync(ListOptions? options = null, RunListOptions? filter = null)` → `PaginationList<ActorRun>`.
- `IterateAsync(ListOptions? options = null, RunListOptions? filter = null)` → `IAsyncEnumerable<ActorRun>`
  (lazy, all pages).

`ListOptions` and `RunListOptions` both live in `Apify.Client.Options`. `ListOptions` fields: `Offset`,
`Limit`, `Desc` (standard pagination). `RunListOptions` fields:
`Status` (`IReadOnlyList<ActorJobStatus>?`, filter by one or more run statuses such as
`ActorJobStatus.Succeeded`/`ActorJobStatus.Running`),
`StartedAfter` and `StartedBefore` (ISO 8601 bounds, honoured only by the Actor- and task-scoped run
collections).

The status filter accepts several `ActorJobStatus` values at once; the filter is the **second** argument
to `ListAsync` (pass `null` for the first to keep default pagination):

```csharp
using System;
using Apify.Client;
using Apify.Client.Models;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var page = await client.Runs().ListAsync(
    new ListOptions { Limit = 10 },
    new RunListOptions { Status = new[] { ActorJobStatus.Succeeded, ActorJobStatus.Running } });
Console.WriteLine("Runs on this page: " + page.Count);
```

## Single run — `client.Run(runId)`

- `GetAsync(int? waitForFinishSecs = null)` → `ActorRun?`.
- `UpdateAsync(object newFields)` → `ActorRun`.
- `DeleteAsync()`.
- `AbortAsync(bool? gracefully = null)` → `ActorRun`.
- `MetamorphAsync(string targetActorId, object? input = null, MetamorphOptions? options = null)` → `ActorRun`.
- `RebootAsync()` → `ActorRun`.
- `ResurrectAsync(RunResurrectOptions? options = null)` → `ActorRun`.
- `ChargeAsync(RunChargeOptions options)` → `Task` — record pay-per-event charges (idempotent).
- `WaitForFinishAsync(int? waitSecs = null)` → `ActorRun`.
- `Dataset()`, `KeyValueStore()`, `RequestQueue()` — the run's default storages.
- `Log()` → `LogClient`; `GetStreamedLogAsync()` → `Stream` (live raw log).
- `GetStreamedLog(Action<string> toLog, bool fromStart = true)` → `StreamedLog` — redirects the run's live
  log to `toLog` one complete message at a time. Call `Start()` to begin and `StopAsync()` (or dispose) to
  end. `fromStart: false` skips messages older than the helper's creation.

### Option and charge types

`MetamorphOptions` fields: `Build` (`string?`, pin the target Actor's build) and `ContentType`
(`string?`, content type of the metamorph `input` body; defaults to `application/json`).

`RunResurrectOptions` overrides run settings when resurrecting a finished run: `Build` (`string?`),
`MemoryMbytes` (`int?`), `TimeoutSecs` (`int?`), `MaxItems` (`int?`), `MaxTotalChargeUsd` (`double?`),
and `RestartOnError` (`bool?`). See [Actors](actors.md) for each field's meaning.

`RunChargeOptions` describes a pay-per-event charge and is built via its constructor
`RunChargeOptions(string eventName, int? count = null, string? idempotencyKey = null)`:

| Property | Type | Description |
|---|---|---|
| `EventName` | `string` | Name of the pay-per-event event to charge for (required, non-empty). |
| `Count` | `int?` | Number of event occurrences to charge (defaults to 1 server-side). |
| `IdempotencyKey` | `string?` | Key that deduplicates retried charges; auto-generated when omitted. |

```csharp
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);

// Read the run's default dataset and key-value store.
var items = await client.Run(run.Id!).Dataset().ListItemsAsync();
var record = await client.Run(run.Id!).KeyValueStore().GetRecordAsync("OUTPUT");

// Charge a pay-per-event run.
await client.Run(run.Id!).ChargeAsync(new RunChargeOptions("result", count: 3));
```
