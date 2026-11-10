# Apify .NET client documentation

See the [top-level README](../README.md) for the client's official-but-experimental status.

A resource-oriented client for the [Apify API](https://docs.apify.com/api/v2), mirroring the
[JavaScript](https://github.com/apify/apify-client-js) reference client: start from an `ApifyClient` and
drill down into resources.

All calls are asynchronous. Every method takes an optional `CancellationToken cancellationToken = default` as
its last parameter, omitted from the signatures in these pages. Method names follow the reference client with
the `Async` suffix (`GetAsync`, `ListAsync`, `CallAsync`, …).

## Contents

- [Actors](actors.md): create, run, build, validate input, Server Actors, versions, and environment variables.
- [Builds](builds.md): fetch, wait, abort, logs, OpenAPI definition.
- [Runs](runs.md): get/wait, abort, metamorph, reboot, resurrect, charge, storages, logs.
- [Storages](storages.md): datasets, key-value stores, request queues.
- [Tasks](tasks.md): pre-configured Actor runs.
- [Schedules](schedules.md)
- [Webhooks](webhooks.md): webhooks and dispatches.
- [Misc](misc.md): the Apify Store, users, logs.
- [Data models](models.md): property reference for every returned model (`Actor`, `ActorRun`, `Build`, `PaginationList<T>`, …).
- [Examples](examples.md): runnable end-to-end examples.

## Requirements

- .NET 8.0 or newer.

## Installation

```bash
dotnet add package Apify.Client
```

## Quick start

The snippets assume `ImplicitUsings` is disabled, so every `using`, `System` included, is listed.

```csharp
using System;
using Apify.Client;

var client = new ApifyClient("my-api-token");

// Start an Actor and wait for it to finish. Pass waitSecs to bound the wait.
var run = await client.Actor("apify/hello-world").CallAsync();

// Read items from the run's default dataset.
var items = await client.Dataset(run.DefaultDatasetId!).ListItemsAsync();
// Items.Count is the number of items on this page; Total is the count across all pages.
Console.WriteLine($"Items on this page: {items.Items.Count} (of {items.Total} total)");
```

The client does not read `APIFY_TOKEN` or any other environment variable; pass the token yourself, for example
`new ApifyClient(Environment.GetEnvironmentVariable("APIFY_TOKEN") ?? throw new InvalidOperationException("Set APIFY_TOKEN"))`.
Get a token in [Apify Console → Settings → API & Integrations](https://console.apify.com/settings/integrations).

## Namespaces

| Namespace | Contents |
|---|---|
| `Apify.Client` | `ApifyClient`, `ApifyClientOptions`, `ApifyClientVersion`, `RequestCompression`, `Statistics`, and `StreamedLog`. |
| `Apify.Client.Resources` | The resource clients the entry point returns (`ActorClient`, `RunClient`, `DatasetClient`, the `…CollectionClient` types, …). |
| `Apify.Client.Models` | The models the clients return (`Actor`, `ActorRun`, `Build`, `PaginationList<T>`, …). |
| `Apify.Client.Options` | The option objects passed to methods (`ActorStartOptions`, `ListOptions`, `SetRecordOptions`, …). |
| `Apify.Client.Exceptions` | `ApifyClientException` and its subclasses. |
| `Apify.Client.Http` | `IHttpTransport`, `HttpClientTransport`, and the request body compressors. |
| `System.Text.Json.Nodes` | Not an Apify namespace, but needed to name `JsonObject` and `JsonNode`, which the clients use for dataset items, inputs, and other free-form data. |

Fluent chains such as `client.Actor("id").Builds()` need only `using Apify.Client;`. Add
`using Apify.Client.Resources;` to name a resource client type, for example in a variable:

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
| `BaseUrl` | `https://api.apify.com` | API base URL, with or without `/v2`. |
| `PublicBaseUrl` | `https://api.apify.com` | Base URL of public, shareable resource URLs, whatever `BaseUrl` is, as in the reference client. |
| `MaxRetries` | `8` | Maximum retries for failed requests. |
| `MinDelayBetweenRetriesMillis` | `500` | Minimum delay between retries (exponential backoff). |
| `MaxDelayBetweenRetriesMillis` | request timeout | Upper bound of the growing delay between retries. |
| `TimeoutSecs` | `360` | The overall per-request cap (the reference client's `timeoutMaxSecs`): every attempt's timeout, a `WithTimeout` override included, is clamped to it. Also the base of the `Long` tier unless `TimeoutLongSecs` is set. |
| `TimeoutLongSecs` | `TimeoutSecs` | Base duration of the `Long` tier: downloads, uploads, and streaming. |
| `TimeoutShortSecs` | `5` | Base duration of the `Short` tier: metadata reads and writes. |
| `TimeoutMediumSecs` | `30` | Base duration of the `Medium` tier: listing, batch, and trigger calls. |
| `UserAgentSuffix` | `null` | Custom suffix appended to the `User-Agent` header. |
| `RequestCompression` | `RequestCompression.Brotli` | Algorithm used to compress request bodies ≥ 1024 bytes: `Brotli` (`Content-Encoding: br`), `Gzip` (`Content-Encoding: gzip`) or `Zstd` (`Content-Encoding: zstd`). |
| `Compressor` | `null` | A custom `HttpCompressor` for another quality or algorithm; takes precedence over `RequestCompression`. |
| `Headers` | `null` | Headers sent with every request, such as an integration marker. They win over the built-in `User-Agent` and `Authorization`; names are case-insensitive. A content header such as `Content-Type` makes the constructor throw. |
| `HttpTransport` | `HttpClientTransport` | The replaceable transport (`Apify.Client.Http.IHttpTransport`). |

Requests are retried with exponential backoff and jitter on network errors, 429, and 5xx responses. Other 4xx
responses throw `ApifyApiException` at once.

### Request statistics

`client.Stats` counts `Calls`, `Requests` (including retries) and `RateLimitErrors` (the `429` answers per attempt:
index 0 is the first attempt, index 1 the first retry, and so on), as the reference client's `stats` does.

### Timeout tiers

Each method picks the timeout tier that fits its request: `Short` for metadata reads and writes, `Medium` for
listing, batch, and trigger calls, `Long` for downloads, uploads, and streaming. The tier durations are set by
`TimeoutShortSecs`, `TimeoutMediumSecs`, and `TimeoutLongSecs`; `TimeoutSecs` is the cap none can exceed.

Every resource client has `WithTimeout(TimeSpan timeout)`, which overrides the tier default for the calls made
through that client instance, for example `client.Dataset(id).WithTimeout(TimeSpan.FromMinutes(5)).DownloadItemsAsync(...)`.
A value above `TimeoutSecs` is capped at it, as in the reference client, and `TimeSpan.Zero` means no timeout.
`RequestQueueClient` (`RequestQueueClientOptions.TimeoutSecs`) and `SetRecordAsync` (`SetRecordOptions.TimeoutSecs`)
have their own narrower overrides.

### Request body compression

Bodies of at least 1024 bytes are compressed before sending, unless their content type already carries its own
compression (images, audio, video, archives). Brotli is the default; set `RequestCompression` to `Gzip` or `Zstd`
to use those.

For another quality, pass a compressor: `BrotliHttpCompressor` takes a quality from 0 to 11 (default 6),
`GzipHttpCompressor` a `System.IO.Compression.CompressionLevel` (default `Optimal`) and `ZstdHttpCompressor` a
quality from 1 to 19 (default 3). To use another encoding the API accepts (`deflate`, `identity`), derive from
`HttpCompressor`: set `ContentEncoding` and implement `CompressAsync`.

```csharp
using Apify.Client;
using Apify.Client.Http;

var client = new ApifyClient(new ApifyClientOptions
{
    Token = "<TOKEN>",
    Compressor = new BrotliHttpCompressor(quality: 11),
});
```

### Replaceable HTTP transport

The default `HttpClientTransport` wraps `HttpClient`. Pass a configured `HttpClient` (proxy, TLS, connection
pool) or your own `IHttpTransport`, for example a mock in tests:

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

Requests to an Actor's server (`client.Actor(id).Server()`, see [Actors](actors.md)) carry the API token in a
header, so they must not follow redirects. They set `ApifyRequestOptions.FollowRedirects` to `false` in
`HttpRequestMessage.Options`, and a custom `IHttpTransport` has to return a redirect response as it is.
`HttpClientTransport` sends them through a second `HttpClient` with automatic redirects disabled. It creates one
itself; a transport wrapping your own `HttpClient` needs one passed as the second constructor argument
(`new HttpClientTransport(httpClient, noRedirectHttpClient)`), otherwise such a request throws
`InvalidOperationException`.

## Error handling

Methods that fetch a single resource return `null` when it does not exist. Other API failures throw
`ApifyApiException`:

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
| `ApiMessage` | `string` | Error message from the API, without the status and type prefix. |
| `Attempt` | `int` | 1-based number of the API-call attempt that produced the error. |
| `HttpMethod` | `string` | HTTP method of the failed call (e.g. `GET`, `POST`). |
| `Path` | `string` | API endpoint path (URL excluding origin). |
| `ErrorData` | `JsonObject?` | Additional structured error data from the API, if any. |

The exception types, all deriving from `ApifyClientException` (so `catch (ApifyClientException)` catches them all):

- `ApifyApiException`: the API answered with an error status. Per-status subclasses (`NotFoundException`, `RateLimitException`, …) let you catch one kind.
- `ApifyTransportException`: no response (network failure, timeout, DNS) after all retries.
- `InvalidResponseBodyException`: a successful response whose body is not JSON.
- `ResponseValidationException`: a successful response whose JSON does not match the specification's schema for the operation (a missing `data` envelope or required property, a wrong type). Unknown properties and enum values are accepted, so new API fields do not break the client.
- `ActorServerException`: an Actor that is not a Server Actor (see [Actors](actors.md)).

`InvalidResponseBodyException` and `ResponseValidationException` are not retried and expose `StatusCode`, `HttpMethod`,
`Path` and the raw `Body`. An error status with an unparsable body throws the `ApifyApiException` of its status, with
the body in its message.

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

- `Apify.Client.ApifyClientVersion.ClientVersion`: the semantic version of this library.
- `Apify.Client.ApifyClientVersion.ApiSpecVersion`: the Apify OpenAPI spec version this client was
  built against.

```csharp
using System;
using Apify.Client;

Console.WriteLine($"{ApifyClientVersion.ClientVersion} / {ApifyClientVersion.ApiSpecVersion}");
```

## License

[Apache-2.0](../LICENSE).
