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
| `ChunkSize` | `int?` | Page size used by `IterateAsync` (positive; the smaller of this and the remaining `Limit` is requested per call). Never sent to the API. |

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
  redirects the run's live log to that sink for the duration of the wait; unlike the reference client,
  omitting it does not redirect the log, so pass `StreamedLog.DefaultConsole` to get the reference's
  default redirection to the console, with its `<Actor name> runId:<id> -> ` prefix). Time spent retrying
  `ActorStartOptions.WaitForResources` does not count toward `waitSecs`.
- `ValidateInputAsync(object? input = null, ValidateInputOptions? options = null)` → `bool`.

`input` on `StartAsync`/`CallAsync`/`ValidateInputAsync` (and `RunClient.MetamorphAsync`, see
[Runs](runs.md)) is a plain object or array serialized to JSON, or raw bytes (`byte[]`) sent as-is —
pass `byte[]` together with `ActorStartOptions.ContentType` to send a pre-serialized or binary body
without re-encoding it as JSON.
- `BuildAsync(string versionNumber, ActorBuildOptions? options = null)` → `Build`.
- `DefaultBuildAsync(int? waitForFinish = null)` → `BuildClient`.
- `LastRun(LastRunOptions? options = null)` → `RunClient` (filter by `Status`/`Origin`).
- `Builds()` → `BuildCollectionClient`; `Runs()` → `RunCollectionClient`.
- `Version(string versionNumber)` → `ActorVersionClient`; `Versions()` → `ActorVersionCollectionClient` — a single Actor version and the version collection.
- `Webhooks()` → read-only `NestedWebhookCollectionClient`.
- `Server()` → `ActorServerClient` — the Actor's server interface, see [Server Actors](#server-actors--clientactoridserver).

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
| `WaitForResources` | `WaitForResources?` | Retry the start while the account has no free memory or concurrent-run slot for it, instead of throwing immediately. `WaitForResources.UntilAvailable` retries until the run starts; `WaitForResources.For(seconds)` bounds the retrying, after which the last error is thrown. `null` (the default) disables retrying. |
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

## Server Actors — `client.Actor(id).Server()`

A Server Actor (formerly Actor Standby) runs a web server that is called over HTTP without starting a run
for each call. `Server()` returns an `ActorServerClient`; every call returns the same client, so the server
URL it fetches is reused.

- `RequestAsync(string path = "/", ActorServerRequestOptions? options = null)` → `ActorServerResponse` —
  sends a request to the Actor's server and returns its response, whatever its status.
- `GetUrlAsync()` → `string` — the server URL (the Actor's `standbyUrl`), to call the server with another
  HTTP client. Fetched from the API on the first call of `GetUrlAsync`, `GetOpenApiSpecAsync` or
  `RequestAsync`, then reused.
- `GetOpenApiSpecAsync()` → `JsonObject?` — the web server schema (OpenAPI specification) of the build that
  serves the server: `actorStandby.build` (a tag or a build number) or `latest`; `null` if the definition has
  none.
- `WithTimeout(TimeSpan timeout)` — the timeout of the API calls made by `GetUrlAsync` and
  `GetOpenApiSpecAsync`.

They throw `ActorServerException` when the Actor is not a Server Actor (or the serving build does not exist),
and the usual `ApifyApiException` when it does not exist. `RequestAsync` also throws
`InvalidOperationException` when the client's `HttpClientTransport` wraps your own `HttpClient` without a
second one that does not follow redirects (see
[Replaceable HTTP transport](README.md#replaceable-http-transport)).

`RequestAsync` details:

- The request goes to the server URL, and `path` is always resolved against it (a fragment is dropped), so
  neither the request nor the token can reach another host.
- The token travels in the `X-Apify-Authorization` header, which the platform removes before the request
  reaches the Actor's code. Headers you pass, `Authorization` included, reach the Actor as they are.
- Any response is returned, error statuses included. Only 429 and 503 are retried (with the client's retry
  settings, returning the last response when they run out), because other statuses can come from the Actor's
  code. Network errors and timeouts are not retried, since the Actor may have already handled the request, and
  a redirect is returned as it is instead of followed.
- Bodies are sent uncompressed unless `Compressor` is set. The timeout defaults to the `Long` tier, which
  covers the cold start of an idle Actor.

`ActorServerRequestOptions` fields:

| Field | Type | Description |
|---|---|---|
| `Method` | `HttpMethod?` | HTTP method. Defaults to `GET`. |
| `Query` | `IReadOnlyDictionary<string, object?>?` | Query parameters; `null` values are left out, booleans become `1`/`0`, dates ISO 8601 in UTC and lists their comma-joined values. |
| `Headers` | `IReadOnlyDictionary<string, string>?` | Additional request headers. A `Content-Type` header equals `ContentType`; a `Content-Encoding` header marks a body that is already encoded and is then not compressed; other `Content-*` headers (e.g. `Content-Disposition`) are sent only with a `Body`. |
| `Body` | `object?` | Request body: `byte[]` and `string` are sent as they are, anything else is serialized to JSON. |
| `ContentType` | `string?` | Content type of the body; defaults to `application/json`, `text/plain; charset=utf-8` for a string and `application/octet-stream` for bytes. |
| `Compressor` | `HttpCompressor?` | Compressor of the body (e.g. `new GzipHttpCompressor()`). Set it only if the Actor's server decodes the resulting `Content-Encoding`. |
| `StreamResponse` | `bool` | Return the response body unread, to read it with `ReadAsStreamAsync`. The response holds its connection until disposed. |
| `Timeout` | `TimeSpan?` | Timeout of the request; defaults to the `Long` tier. |

`ActorServerResponse` is `IDisposable` (dispose it when done) and has `StatusCode`, `IsSuccess`, `Headers`
(case-insensitive), `ContentType`, `ReadAsBytesAsync()`, `ReadAsStringAsync()`, `ReadAsStreamAsync()` and
`ReadAsJsonAsync()` (`JsonNode?`; throws `InvalidResponseBodyException` for a body that is not JSON).

```csharp
using System;
using System.Collections.Generic;
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var server = client.Actor("username/actor-name").Server();

using var response = await server.RequestAsync(
    "/search",
    new ActorServerRequestOptions { Query = new Dictionary<string, object?> { ["q"] = "apify" } });
if (response.IsSuccess)
{
    Console.WriteLine(await response.ReadAsJsonAsync());
}

Console.WriteLine(await server.GetUrlAsync());
Console.WriteLine(await server.GetOpenApiSpecAsync());
```

## Versions and environment variables

Manage an Actor's versions with `client.Actor(id).Versions()` (the whole collection) and
`client.Actor(id).Version(versionNumber)` (one version). Each version in turn owns a collection of
environment variables, reached with `.EnvVars()` / `.EnvVar(name)`.

### Version collection — `client.Actor(id).Versions()` → `ActorVersionCollectionClient`

The endpoint returns every version in one response and does not support pagination, so unlike most
collection clients these methods take no filtering/paging options.

- `ListAsync()` — list the Actor's versions. Returns `PaginationList<ActorVersion>`.
- `IterateAsync()` → `IAsyncEnumerable<ActorVersion>` — iterate the versions; exists for API parity
  with the other collection iterators (the endpoint has only the one page).
- `CreateAsync(object version)` — create a version from any JSON-serializable definition. Returns
  `ActorVersion`.

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
