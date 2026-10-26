# Runs

Access the account-wide run collection with `client.Runs()`, an Actor's or task's runs with
`client.Actor(id).Runs()` / `client.Task(id).Runs()`, and a specific run with `client.Run(runId)`.

## Collection

- `ListAsync(ListOptions? options = null, RunListOptions? filter = null)` → `PaginationList<ActorRun>`.
- `IterateAsync(ListOptions? options = null, RunListOptions? filter = null)` → `IAsyncEnumerable<ActorRun>`
  (lazy, all pages).

`ListOptions` fields: `Offset`, `Limit`, `Desc` (standard pagination; `Limit` `0` or unset means no cap) and `ChunkSize` (the page size `IterateAsync` requests; the smaller of it and the remaining `Limit` is used per call). `RunListOptions` fields:
`Status` (`IReadOnlyList<string>?`, filter by one or more run statuses such as `SUCCEEDED`/`RUNNING`),
`StartedAfter` and `StartedBefore` (ISO 8601 bounds, honoured only by the Actor- and task-scoped run
collections).

## Single run — `client.Run(runId)`

- `GetAsync(int? waitForFinishSecs = null)` → `ActorRun?`.
- `UpdateAsync(object newFields)` → `ActorRun`.
- `DeleteAsync()`.
- `AbortAsync(bool? gracefully = null)` → `ActorRun`.
- `MetamorphAsync(string targetActorId, object? input = null, MetamorphOptions? options = null)` → `ActorRun`.
- `RebootAsync()` → `ActorRun`.
- `ResurrectAsync(RunResurrectOptions? options = null)` → `ActorRun`. Resurrects a finished run (status `SUCCEEDED`, `FAILED`, `ABORTED` or `TIMED-OUT`) and restarts it with the same storages.
- `ChargeAsync(RunChargeOptions options)` → `Task` — record pay-per-event charges (idempotent).
- `WaitForFinishAsync(int? waitSecs = null)` → `ActorRun` — client-side polling until terminal (`null`
  waits indefinitely). A run id that keeps answering 404 for 3 seconds straight (replica lag resolves
  faster than that) throws `NotFoundException` instead of polling for the rest of `waitSecs`.
- `IterateDatasetItemsAsync(RunIterateDatasetItemsOptions? options = null)` → `IAsyncEnumerable<JsonNode?>` —
  lazily iterates the run's default dataset while the run is still producing items: it reads the rows
  below the dataset's `itemCount`, waits for the run to finish between polls, and after the run finishes
  reads any remaining rows. On a `LastRun()` client it sticks to the run its first request resolved to.
- `Dataset()`, `KeyValueStore()`, `RequestQueue()` — the run's default storages. Their own
  `GetAsync()`/`DeleteAsync()` throw on a 404 instead of resolving `null`/no-op, since the run itself
  (not just the storage) could be what's missing; a record/request looked up by its own key or id
  (e.g. `GetRecordAsync`, `GetRequestAsync`) is unaffected and still resolves `null` for a missing one.
- `Log()` → `LogClient` (its `GetAsync`/`StreamAsync` methods are documented under [Logs in misc.md](misc.md#logs--clientlogbuildorrunid); on this run-nested client they throw rather than resolve `null`, for the same reason); `GetStreamedLogAsync()` → `Stream` (live raw log).
- `GetStreamedLog(Action<string> toLog, bool fromStart = true)` → `StreamedLog` — redirects the run's live
  log to `toLog` one complete message at a time. Call `Start()` to begin and `StopAsync()` (or dispose) to
  end. `fromStart: false` skips messages older than the helper's creation.

### Option and charge types

`MetamorphOptions` fields: `Build` (`string?`, pin the target Actor's build) and `ContentType`
(`string?`, content type of the metamorph `input` body; defaults to `application/json`).

`RunResurrectOptions` overrides run settings when resurrecting a finished run: `Build` (`string?`),
`MemoryMbytes` (`int?`), `TimeoutSecs` (`int?`), `MaxItems` (`int?`), `MaxTotalChargeUsd` (`double?`),
and `RestartOnError` (`bool?`). See [Actors](actors.md) for each field's meaning.

`RunIterateDatasetItemsOptions` fields (all optional): `Offset` (rows to skip), `Limit` (maximum rows to
scan; `null` or `0` for no limit), `ChunkSize` (rows per API call, default 1000), `PollIntervalSecs`
(seconds to wait for the run to finish between polls, default 5), and the item filters `Clean`, `Fields`,
`Omit`, `Unwind`, `SkipEmpty` and `SkipHidden` (same meaning as in `DatasetListItemsOptions`).

```csharp
using System;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var run = await client.Actor("apify/hello-world").StartAsync();

// Items are yielded as the run produces them, until the run has finished and its dataset is drained.
await foreach (var item in client.Run(run.Id!).IterateDatasetItemsAsync(new RunIterateDatasetItemsOptions { ChunkSize = 100 }))
{
    Console.WriteLine(item);
}
```

`RunChargeOptions` describes a pay-per-event charge and is built via its constructor
`RunChargeOptions(string eventName, int? count = null, string? idempotencyKey = null)`:

| Property | Type | Description |
|---|---|---|
| `EventName` | `string` | Name of the pay-per-event event to charge for (required, non-empty). |
| `Count` | `int?` | Number of event occurrences to charge (defaults to 1). |
| `IdempotencyKey` | `string?` | Key that deduplicates retried charges; auto-generated when omitted. |
| `RequestId` | `string?` | ID of the Server Actor request being charged (the `X-Actor-Request-ID` header of the incoming request); required for multi-tenant Server Actor runs. Set via object initializer. |

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
