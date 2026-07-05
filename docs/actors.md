# Actors

Access the Actor collection with `client.Actors()` and a specific Actor with `client.Actor(id)`, where
`id` is the Actor ID or the `username~name` form.

## Collection — `client.Actors()`

- `ListAsync(ActorListOptions? options = null)` — list the account's Actors (one page). Returns
  `PaginationList<Actor>`.
- `IterateAsync(ActorListOptions? options = null)` → `IAsyncEnumerable<Actor>` — lazily iterate every
  Actor across pages, fetching each page on demand.
- `CreateAsync(object actor)` — create an Actor from any JSON-serializable definition. Returns `Actor`.

`ActorListOptions` fields:

| Field | Type | Description |
|---|---|---|
| `Offset` | `int?` | Number of Actors to skip from the start. |
| `Limit` | `int?` | Maximum number of Actors to return in the page. |
| `Desc` | `bool?` | Sort newest-first when `true`. |
| `My` | `bool?` | Return only Actors owned by the current account when `true`. |
| `SortBy` | `string?` | Field to sort by (e.g. `createdAt`, `lastRunStartedAt`). |

```csharp
using System;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var page = await client.Actors().ListAsync(new ActorListOptions { My = true, Limit = 10 });
foreach (var actor in page.Items)
{
    Console.WriteLine(actor.Name);
}
```

## Single Actor — `client.Actor(id)`

- `GetAsync()` → `Actor?` (null if not found).
- `UpdateAsync(object newFields)` → `Actor`.
- `DeleteAsync()`.
- `StartAsync(object? input = null, ActorStartOptions? options = null)` → `ActorRun` (returns immediately).
- `CallAsync(object? input = null, ActorStartOptions? options = null, int? waitSecs = null, Action<string>? log = null)`
  → `ActorRun` (starts then waits; `waitSecs` bounds the wait, `null` waits indefinitely; `log`, if set,
  redirects the run's live log to that sink for the duration of the wait).
- `ValidateInputAsync(object? input = null, ValidateInputOptions? options = null)` → `bool`.
- `BuildAsync(string versionNumber, ActorBuildOptions? options = null)` → `Build`.
- `DefaultBuildAsync(int? waitForFinish = null)` → `BuildClient`.
- `LastRun(LastRunOptions? options = null)` → `RunClient` (filter by `Status`/`Origin`).
- `Builds()` → `BuildCollectionClient`; `Runs()` → `RunCollectionClient`.
- `Version(string versionNumber)` / `Versions()` — Actor versions.
- `Webhooks()` → read-only `NestedWebhookCollectionClient`.

`ActorStartOptions` fields:

| Field | Type | Description |
|---|---|---|
| `Build` | `string?` | Tag or number of the Actor build to run (e.g. `latest`). |
| `MemoryMbytes` | `int?` | Memory limit for the run, in megabytes. |
| `TimeoutSecs` | `int?` | Hard run timeout in seconds (`0` means no limit). |
| `WaitForFinish` | `int?` | Seconds the *start* request itself blocks server-side waiting for the run (max 60). |
| `MaxItems` | `int?` | Maximum number of dataset items the (pay-per-result) run may produce. |
| `MaxTotalChargeUsd` | `double?` | Maximum total USD the run is allowed to charge. |
| `ContentType` | `string?` | Content type of the `input` body (defaults to `application/json`). |
| `RestartOnError` | `bool?` | Automatically restart the run's container if it exits with an error. |
| `ForcePermissionLevel` | `string?` | Override the Actor's permission level (`LIMITED_PERMISSIONS`/`FULL_PERMISSIONS`). |
| `Webhooks` | `object?` | Ad-hoc webhooks (any JSON-serializable list) to attach to this run. |

```csharp
using System;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var run = await client.Actor("apify/hello-world").CallAsync(
    new { message = "hi" },
    new ActorStartOptions { MemoryMbytes = 256, Build = "latest" },
    120);
Console.WriteLine(run.Status);
```

`ValidateInputOptions` fields: `Build` (`string?`, the Actor build whose input schema to validate
against) and `ContentType` (`string?`, content type of the input; defaults to `application/json`).

`LastRunOptions` fields: `Status` (`string?`, only consider the last run with this status, e.g.
`SUCCEEDED`) and `Origin` (`string?`, only consider the last run started from this origin, e.g. `API`).

## Versions and environment variables

```csharp
using Apify.Client;
using Apify.Client.Models;

var client = new ApifyClient("my-api-token");
var actor = client.Actor("me/my-actor");

await actor.Versions().CreateAsync(new
{
    versionNumber = "0.1",
    sourceType = "SOURCE_FILES",
    buildTag = "latest",
    sourceFiles = System.Array.Empty<object>(),
});

var envVars = actor.Version("0.1").EnvVars();
await envVars.CreateAsync(new ActorEnvVar("MY_VAR", "value", isSecret: true));
await actor.Version("0.1").EnvVar("MY_VAR").DeleteAsync();
```
