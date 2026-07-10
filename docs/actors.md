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
- `Version(string versionNumber)` → `ActorVersionClient`; `Versions()` → `ActorVersionCollectionClient` — a single Actor version and the version collection.
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

Manage an Actor's versions with `client.Actor(id).Versions()` (the whole collection) and
`client.Actor(id).Version(versionNumber)` (one version). Each version in turn owns a collection of
environment variables, reached with `.EnvVars()` / `.EnvVar(name)`.

### Version collection — `client.Actor(id).Versions()` → `ActorVersionCollectionClient`

- `ListAsync(ListOptions? options = null)` — list the Actor's versions (one page). Returns
  `PaginationList<ActorVersion>`.
- `IterateAsync(ListOptions? options = null)` → `IAsyncEnumerable<ActorVersion>` — lazily iterate every
  version across pages, fetching each page on demand.
- `CreateAsync(object version)` — create a version from any JSON-serializable definition. Returns
  `ActorVersion`.

`ListOptions` fields: `Offset` (`int?`, items to skip), `Limit` (`int?`, page size), `Desc` (`bool?`,
newest-first when `true`).

### Single version — `client.Actor(id).Version(versionNumber)` → `ActorVersionClient`

`versionNumber` is the version identifier (e.g. `0.1`).

- `GetAsync()` → `ActorVersion?` (null if not found).
- `UpdateAsync(object newFields)` → `ActorVersion` — update with any JSON-serializable set of fields.
- `DeleteAsync()`.
- `EnvVars()` → `ActorEnvVarCollectionClient` — this version's environment-variable collection.
- `EnvVar(string name)` → `ActorEnvVarClient` — a single environment variable of this version.

### Env-var collection — `Version(versionNumber).EnvVars()` → `ActorEnvVarCollectionClient`

- `ListAsync()` — list the version's environment variables (the endpoint returns them in a single page).
  Returns `PaginationList<ActorEnvVar>`.
- `IterateAsync()` → `IAsyncEnumerable<ActorEnvVar>` — iterate the variables; provided for parity with
  the other collection iterators (yields the single page's items).
- `CreateAsync(ActorEnvVar envVar)` → `ActorEnvVar` — create an environment variable.

### Single env-var — `Version(versionNumber).EnvVar(name)` → `ActorEnvVarClient`

`name` is the environment variable's name.

- `GetAsync()` → `ActorEnvVar?` (null if not found).
- `UpdateAsync(ActorEnvVar envVar)` → `ActorEnvVar`.
- `DeleteAsync()`.

See [`ActorVersion`](models.md#actorversion) and [`ActorEnvVar`](models.md#actorenvvar) for the returned
models and the `ActorEnvVar` constructor used below.

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
