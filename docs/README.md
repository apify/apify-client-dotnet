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

// Start an Actor and wait for it to finish. Pass waitSecs (e.g. 120) to bound the wait;
// by default it waits indefinitely.
var run = await client.Actor("apify/hello-world").CallAsync();

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
| `Apify.Client.Options` | The option/request objects passed into methods — `ActorStartOptions`, `ActorBuildOptions`, `ActorListOptions`, `RunListOptions`, `RunResurrectOptions`, `RunChargeOptions`, `MetamorphOptions`, `LastRunOptions`, `DatasetListItemsOptions`, `DatasetDownloadOptions`, `DownloadItemsFormat`, `GetRecordOptions`, `SetRecordOptions`, `ListKeysOptions`, `IterateKeysOptions`, `ListRequestsOptions`, `PaginateRequestsOptions`, `BatchAddRequestsOptions`, `RequestQueueClientOptions`, `TaskStartOptions`, `ValidateInputOptions`, `LogOptions`, `ListOptions`, `StorageListOptions`, `StoreListOptions`. |
| `Apify.Client.Exceptions` | `ApifyClientException` (common base), `ApifyApiException` (and its per-status subclasses), `ApifyTransportException`, `InvalidResponseBodyException` and `ResponseValidationException`. |
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
| `BaseUrl` | `https://api.apify.com` | API base URL, with or without the `/v2` version path — it's appended only when not already present. |
| `PublicBaseUrl` | `https://api.apify.com` | Base URL used when building public, shareable resource URLs, whatever `BaseUrl` is (as in the reference client). Also accepted with or without `/v2`. |
| `MaxRetries` | `8` | Maximum retries for failed requests. |
| `MinDelayBetweenRetriesMillis` | `500` | Minimum delay between retries (exponential backoff). |
| `MaxDelayBetweenRetriesMillis` | request timeout | Upper bound on the growing inter-retry delay. |
| `TimeoutSecs` | `360` | The overall per-request cap (the reference client's `timeoutMaxSecs`): every attempt's growing timeout, including a `WithTimeout` override, is clamped to it, regardless of tier. Also the base of the `Long` tier unless `TimeoutLongSecs` is set. |
| `TimeoutLongSecs` | `TimeoutSecs` | Base duration of the `Long` timeout tier: downloads, uploads, streaming. Matches the reference client's `timeoutLongSecs`. |
| `TimeoutShortSecs` | `5` | Base duration of the `Short` timeout tier: simple metadata reads/writes (get/update/delete a resource). |
| `TimeoutMediumSecs` | `30` | Base duration of the `Medium` timeout tier: listing, batch and trigger calls. |
| `UserAgentSuffix` | `null` | Custom suffix appended to the `User-Agent` header. |
| `RequestCompression` | `RequestCompression.Brotli` | Algorithm used to compress request bodies ≥ 1024 bytes: `Brotli` (`Content-Encoding: br`) or `Gzip` (`Content-Encoding: gzip`). |
| `Compressor` | `null` | A custom `HttpCompressor` for a non-default compression quality or another algorithm; takes precedence over `RequestCompression`. |
| `Headers` | `null` | Additional headers sent with every request, such as an integration platform marker. They win over the built-in `User-Agent` and `Authorization`; header names are case-insensitive, and `client.UserAgent` reports a `User-Agent` override. |
| `HttpTransport` | `HttpClientTransport` | The replaceable transport (`Apify.Client.Http.IHttpTransport`). |

Requests are retried on network errors, HTTP 429 (rate limit) and 5xx responses, with exponential
backoff and jitter. 4xx responses (other than 429) are thrown immediately as `ApifyApiException`.

### Request statistics

`client.Stats` counts what the client has done, as the reference client's `stats` does: `Calls` (client calls),
`Requests` (HTTP requests, including retries) and `RateLimitErrors` (a snapshot list of the `429` answers per
attempt: index 0 holds the first-attempt errors, index 1 the first retry's, and so on).

### Timeout tiers

Each method internally picks the timeout tier that fits the expected duration of its request — `Short`
for metadata reads and writes, `Medium` for listing/batch/trigger calls, `Long` for downloads, uploads
and streaming — so the default wait before giving up is consistent across the client instead of a
single one-size-fits-all budget. The three tiers' durations are configured once, on the options above
(`TimeoutShortSecs`/`TimeoutMediumSecs`/`TimeoutLongSecs`); `TimeoutSecs` is the cap that none of them can exceed.

Every resource client (`client.Actor(id)`, `client.Dataset(id)`, `client.Actors()`, and so on — the
return value of every accessor on `ApifyClient`) exposes `WithTimeout(TimeSpan timeout)`, a general
per-call(-instance) override: call it right after the accessor to make every call made through that
client instance use `timeout` instead of its tier default, for exactly one use of that client, e.g.
`client.Dataset(id).WithTimeout(TimeSpan.FromMinutes(5)).DownloadItemsAsync(...)`. A value above
`TimeoutSecs` (the overall budget) is capped at it, matching the reference client — raise `TimeoutSecs`
itself for a longer per-call timeout; a shorter override still narrows the cap, as before. Pass
`TimeSpan.Zero` for no timeout at all, matching the reference client's `'noTimeout'`. `RequestQueueClient`
(via `client.RequestQueue(id, new RequestQueueClientOptions {
TimeoutSecs = ... })`) and `SetRecordAsync` (via `SetRecordOptions.TimeoutSecs`) already had their own
equivalent, narrower overrides before `WithTimeout` existed and keep them instead.

### Request body compression

Request bodies of at least 1024 bytes are compressed before sending, unless their content type already
carries its own compression (images, audio, video, archives). Brotli is used by default; set
`RequestCompression = RequestCompression.Gzip` to send gzip-compressed bodies instead.

For a different quality, pass a compressor: `BrotliHttpCompressor` takes a quality from 0 (the fastest) to 11
(the best compression, default 6) and `GzipHttpCompressor` a `System.IO.Compression.CompressionLevel`
(`Fastest`, `Optimal` or `SmallestSize`; default `Optimal`). Derive from `HttpCompressor` to use another
algorithm the API accepts (`deflate`, `identity`): set `ContentEncoding` and implement `CompressAsync`.

```csharp
using Apify.Client;
using Apify.Client.Http;

var client = new ApifyClient(new ApifyClientOptions
{
    Token = "<TOKEN>",
    Compressor = new BrotliHttpCompressor(quality: 11),
});
```

### Default headers

`ApifyClientOptions.Headers` adds headers to every request, for example an integration platform marker. They
replace the built-in `User-Agent` and `Authorization` headers when named the same, compared
case-insensitively. Only request headers are allowed; a content header such as `Content-Type` makes the
`ApifyClient` constructor throw, because the client sets those per request.

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

`InvalidResponseBodyException` is thrown when a successful response's body is not valid JSON, and
`ResponseValidationException` when it is valid JSON but does not match the schema the API specification
declares for that operation (for example the `data` envelope or a required property is missing, or a
property has the wrong JSON type). Unknown properties and enum values are accepted, so new API fields do
not break the client. The response arrived in full, so neither is
retried. Both expose `StatusCode`, `HttpMethod`, `Path` and the raw `Body`. An error status with an
unparsable body throws the `ApifyApiException` for its status code, with the raw body in its message.

`ApifyApiException`, `ApifyTransportException`, `InvalidResponseBodyException` and
`ResponseValidationException` extend the common
`ApifyClientException`, so `catch (ApifyClientException)` catches every error this client raises for a
failed call without naming the concrete types:

```csharp
using System;
using Apify.Client;
using Apify.Client.Exceptions;

var client = new ApifyClient("my-api-token");
try
{
    var user = await client.Me().GetAsync();
    Console.WriteLine(user?.Username);
}
catch (ApifyClientException e)
{
    Console.WriteLine("client call failed: " + e.Message);
}
```

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
