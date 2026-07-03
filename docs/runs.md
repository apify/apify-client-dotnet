# Runs

Access the account-wide run collection with `client.Runs()`, an Actor's or task's runs with
`client.Actor(id).Runs()` / `client.Task(id).Runs()`, and a specific run with `client.Run(runId)`.

## Collection

- `ListAsync(ListOptions? options = null, RunListOptions? filter = null)` → `PaginationList<ActorRun>`.
  `RunListOptions`: `Status` (list), `StartedAfter`, `StartedBefore`.

## Single run — `client.Run(runId)`

- `GetAsync(int? waitForFinishSecs = null)` → `ActorRun?`.
- `UpdateAsync(object newFields)` → `ActorRun`.
- `DeleteAsync()`.
- `AbortAsync(bool? gracefully = null)` → `ActorRun`.
- `MetamorphAsync(string targetActorId, object? input = null, MetamorphOptions? options = null)` → `ActorRun`.
- `RebootAsync()` → `ActorRun`.
- `ResurrectAsync(RunResurrectOptions? options = null)` → `ActorRun`.
- `ChargeAsync(RunChargeOptions options)` — record pay-per-event charges (idempotent).
- `WaitForFinishAsync(int? waitSecs = null)` → `ActorRun`.
- `Dataset()`, `KeyValueStore()`, `RequestQueue()` — the run's default storages.
- `Log()` → `LogClient`; `GetStreamedLogAsync()` → `Stream` (live raw log).

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
