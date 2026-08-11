# Apify .NET client documentation

See the [top-level README](../README.md) for the client's official-but-experimental status.

A resource-oriented .NET client for the [Apify API](https://docs.apify.com/api/v2), mirroring the
official [JavaScript](https://github.com/apify/apify-client-js) reference client: start from an
`ApifyClient`, then drill down into resources.

All API calls are asynchronous and return `Task`/`Task<T>`; every method takes an optional
`CancellationToken cancellationToken = default` as its final parameter (omitted from the reference
signatures on the pages below for brevity). Method names mirror the reference client with the .NET
`Async` suffix (`GetAsync`, `ListAsync`, `CallAsync`, …).

## Contents

- [Actors](actors.md) — create, run, build, validate input, versions and environment variables.
- [Builds](builds.md) — fetch, wait, abort, logs, OpenAPI definition.
- [Runs](runs.md) — get/wait, abort, metamorph, reboot, resurrect, charge, storages, logs.
- [Storages](storages.md) — datasets, key-value stores, request queues.
- [Tasks](tasks.md) — pre-configured Actor runs.
- [Schedules](schedules.md)
- [Webhooks](webhooks.md) — webhooks and dispatches.
- [Misc](misc.md) — the Apify Store, users, logs.
- [Data models](models.md) — property reference for every returned model (`Actor`, `ActorRun`, `Build`, `PaginationList<T>`, …).
- [Examples](examples.md) — runnable end-to-end examples.

## Requirements

- .NET 8.0 or newer.

## Installation

```bash
dotnet add package Apify.Client
```

## Quick start

All snippets in this documentation assume `ImplicitUsings` is disabled (the repository's convention),
so every `using` — even `System` — is listed explicitly and appears before any top-level statement.

```csharp
using System;
using Apify.Client;

var client = new ApifyClient("my-api-token");

// Start an Actor and wait for it to finish. The last argument is the wait budget in seconds;
// pass a value (e.g. 120) to bound the wait, or null to wait indefinitely (as here).
var run = await client.Actor("apify/hello-world").CallAsync(null, null, null);

// Read items from the run's default dataset.
var items = await client.Dataset(run.DefaultDatasetId!).ListItemsAsync();
// Count is the number of items in THIS page; Total is the dataset's full count across all pages.
Console.WriteLine($"Items on this page: {items.Count} (of {items.Total} total)");
```

`new ApifyClient("my-api-token")` takes the token as an explicit argument — it does **not** read
`APIFY_TOKEN` (or any other environment variable) automatically. Read it yourself if you want that,
e.g. `new ApifyClient(Environment.GetEnvironmentVariable("APIFY_TOKEN") ?? throw new InvalidOperationException("Set APIFY_TOKEN"))`
(the null-coalescing throw keeps the call null-safe when the variable is unset).

Get your API token from the
[Apify Console → Settings → API & Integrations](https://console.apify.com/settings/integrations).

## Namespaces

The public API is spread across a small set of namespaces. Because `ImplicitUsings` is disabled, add
the `using` directives for whichever ones a file references:

| Namespace | What lives here |
|---|---|
| `Apify.Client` | The entry point (`ApifyClient`), `ApifyClientOptions`, `ApifyClientVersion`, and the log-redirection helper `StreamedLog`. |
| `Apify.Client.Resources` | Every resource client the entry point returns — `ActorClient`, `RunClient`, `BuildClient`, `DatasetClient`, `KeyValueStoreClient`, `RequestQueueClient`, `TaskClient`, `ScheduleClient`, `LogClient`, `UserClient`, `ActorVersionClient`, `ActorEnvVarClient`, the `…CollectionClient` types (including `ActorVersionCollectionClient`, `ActorEnvVarCollectionClient`, `NestedWebhookCollectionClient`, and `WebhookDispatchCollectionClient`), etc. |
| `Apify.Client.Models` | Data models returned by the clients — `Actor`, `ActorRun`, `Build`, `Dataset`, `RequestQueueRequest`, `ActorEnvVar`, `PaginationList<T>`, and so on. |
| `Apify.Client.Options` | The option/request objects passed into methods — `ActorStartOptions`, `ActorBuildOptions`, `ActorListOptions`, `RunListOptions`, `RunResurrectOptions`, `RunChargeOptions`, `MetamorphOptions`, `LastRunOptions`, `DatasetListItemsOptions`, `DatasetDownloadOptions`, `DownloadItemsFormat`, `GetRecordOptions`, `SetRecordOptions`, `ListKeysOptions`, `ListRequestsOptions`, `PaginateRequestsOptions`, `BatchAddRequestsOptions`, `RequestQueueClientOptions`, `TaskStartOptions`, `ValidateInputOptions`, `LogOptions`, `ListOptions`, `StorageListOptions`, `StoreListOptions`. |
| `Apify.Client.Exceptions` | `ApifyApiException` and `ApifyTransportException`. |
| `Apify.Client.Http` | The replaceable transport: `IHttpTransport` and the default `HttpClientTransport`. |
| `System.Text.Json.Nodes` (BCL) | Not an Apify namespace, but required whenever you name the JSON escape-hatch types the client returns/accepts — `JsonObject`/`JsonNode` (e.g. dataset items, `GetInputAsync`/`UpdateInputAsync`, `GetStatisticsAsync`, `GetOpenApiDefinitionAsync`, `MonthlyUsageAsync`/`LimitsAsync`, `RequestQueueRequest.UserData`). Add `using System.Text.Json.Nodes;`. |

Fluent chains such as `client.Actor("id").Builds()` compile with only `using Apify.Client;` because the
intermediate types are inferred. You only need `using Apify.Client.Resources;` when you name a resource
client type explicitly — e.g. storing one in a variable or field:

```csharp
using Apify.Client;
using Apify.Client.Resources;

var client = new ApifyClient("my-api-token");
BuildClient defaultBuild = await client.Actor("apify/hello-world").DefaultBuildAsync();
RunClient lastRun = client.Actor("apify/hello-world").LastRun();
```

## Configuration

Pass an `ApifyClientOptions` to configure non-default settings:

```csharp
using Apify.Client;

var configured = new ApifyClient(new ApifyClientOptions
{
    Token = "my-api-token",
    MaxRetries = 5,
    MinDelayBetweenRetriesMillis = 1000,
    TimeoutSecs = 120,
    UserAgentSuffix = "my-app/1.2.3",
});
```

| Option | Default | Meaning |
|---|---|---|
| `Token` | `null` | API token, sent as a Bearer token. |
| `BaseUrl` | `https://api.apify.com` | API base URL; the `/v2` suffix is appended automatically. |
| `PublicBaseUrl` | `BaseUrl` | Base URL used when building public, shareable resource URLs. |
| `MaxRetries` | `8` | Maximum retries for failed requests. |
| `MinDelayBetweenRetriesMillis` | `500` | Minimum delay between retries (exponential backoff). |
| `MaxDelayBetweenRetriesMillis` | request timeout | Upper bound on the growing inter-retry delay. |
| `TimeoutSecs` | `360` | Overall per-request timeout. |
| `UserAgentSuffix` | `null` | Custom suffix appended to the `User-Agent` header. |
| `RequestCompression` | `RequestCompression.Brotli` | Algorithm used to compress request bodies ≥ 1024 bytes: `Brotli` (`Content-Encoding: br`) or `Gzip` (`Content-Encoding: gzip`). |
| `HttpTransport` | `HttpClientTransport` | The replaceable transport (`Apify.Client.Http.IHttpTransport`). |

Requests are retried on network errors, HTTP 429 (rate limit) and 5xx responses, with exponential
backoff and jitter. 4xx responses (other than 429) are thrown immediately as `ApifyApiException`.

Request bodies of at least 1024 bytes are compressed before sending. Brotli is used by default; set
`RequestCompression = RequestCompression.Gzip` to send gzip-compressed bodies instead.

### Replaceable HTTP transport

The transport is `Apify.Client.Http.IHttpTransport`. The default is `HttpClientTransport`, which wraps
`System.Net.Http.HttpClient`; you can pass a pre-configured `HttpClient` (proxy, TLS, connection pool)
or provide your own `IHttpTransport` (e.g. a mock in tests):

```csharp
using System.Net.Http;
using Apify.Client;
using Apify.Client.Http;

var httpClient = new HttpClient();
var client = new ApifyClient(new ApifyClientOptions
{
    Token = "my-api-token",
    HttpTransport = new HttpClientTransport(httpClient),
});
```

## Error handling

Methods that fetch a single resource return `null` when the resource does not exist (rather than
throwing). Other API failures are thrown as `Apify.Client.Exceptions.ApifyApiException`, which exposes
the HTTP status, API error `Type`, message, attempt count, and request method/path:

```csharp
using System;
using Apify.Client;
using Apify.Client.Exceptions;

var client = new ApifyClient("my-api-token");
try
{
    await client.Actor("does/not-exist").UpdateAsync(new { title = "x" });
}
catch (ApifyApiException e)
{
    Console.WriteLine($"{e.StatusCode} {e.Type}: {e.ApiMessage}");
}
```

`ApifyApiException` members:

| Member | Type | Description |
|---|---|---|
| `StatusCode` | `int` | HTTP status code of the error response. |
| `Type` | `string?` | Machine-readable API error type (e.g. `record-not-found`). |
| `ApiMessage` | `string` | Raw error message from the API, without the status/type prefix. |
| `Attempt` | `int` | 1-based number of the API-call attempt that produced the error. |
| `HttpMethod` | `string` | HTTP method of the failed call (e.g. `GET`, `POST`). |
| `Path` | `string` | API endpoint path (URL excluding origin). |
| `ErrorData` | `JsonObject?` | Additional structured error data from the API, if any. |

`ApifyTransportException` is thrown instead when the request never reaches the API (network failure,
timeout, or DNS error) after all retries are exhausted.

## Versioning

- `Apify.Client.ApifyClientVersion.ClientVersion` — the semantic version of this library.
- `Apify.Client.ApifyClientVersion.ApiSpecVersion` — the Apify OpenAPI spec version this client was
  built against.

```csharp
using System;
using Apify.Client;

Console.WriteLine($"{ApifyClientVersion.ClientVersion} / {ApifyClientVersion.ApiSpecVersion}");
```

## License

[Apache-2.0](../LICENSE).
