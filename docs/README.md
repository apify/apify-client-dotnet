# Apify .NET client documentation

> **Official, but experimental — AI-generated and AI-maintained.** This is an official Apify client,
> but it is experimental: it is generated and maintained by AI. Review the code before relying on it in
> production and report issues on the repository.

A resource-oriented .NET client for the [Apify API](https://docs.apify.com/api/v2), mirroring the
official [JavaScript](https://github.com/apify/apify-client-js) reference client: start from an
`ApifyClient`, then drill down into resources.

All API calls are asynchronous and return `Task`/`Task<T>`; every method accepts an optional
`CancellationToken`. Method names mirror the reference client with the .NET `Async` suffix
(`GetAsync`, `ListAsync`, `CallAsync`, …).

## Contents

- [Actors](actors.md) — create, run, build, validate input, versions and environment variables.
- [Builds](builds.md) — fetch, wait, abort, logs, OpenAPI definition.
- [Runs](runs.md) — get/wait, abort, metamorph, reboot, resurrect, charge, storages, logs.
- [Storages](storages.md) — datasets, key-value stores, request queues.
- [Tasks](tasks.md) — pre-configured Actor runs.
- [Schedules](schedules.md)
- [Webhooks](webhooks.md) — webhooks and dispatches.
- [Misc](misc.md) — the Apify Store, users, logs.
- [Examples](examples.md) — runnable end-to-end examples.

## Requirements

- .NET 8.0 or newer.

## Installation

```bash
dotnet add package Apify.Client
```

## Quick start

```csharp
using Apify.Client;

var client = new ApifyClient("my-api-token");

// Start an Actor and wait for it to finish. The last argument is the wait budget in seconds;
// pass a value (e.g. 120) to bound the wait, or null to wait indefinitely (as here).
var run = await client.Actor("apify/hello-world").CallAsync(null, null, null);

// Read items from the run's default dataset.
var items = await client.Dataset(run.DefaultDatasetId!).ListItemsAsync();
Console.WriteLine("Item count: " + items.Count);
```

`new ApifyClient("my-api-token")` takes the token as an explicit argument — it does **not** read
`APIFY_TOKEN` (or any other environment variable) automatically. Read it yourself if you want that,
e.g. `new ApifyClient(Environment.GetEnvironmentVariable("APIFY_TOKEN"))`.

Get your API token from the
[Apify Console → Settings → API & Integrations](https://console.apify.com/settings/integrations).

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
| `HttpTransport` | `HttpClientTransport` | The replaceable transport (`Apify.Client.Http.IHttpTransport`). |

Requests are retried on network errors, HTTP 429 (rate limit) and 5xx responses, with exponential
backoff and jitter. 4xx responses (other than 429) are thrown immediately as `ApifyApiException`.

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

## Versioning

- `Apify.Client.ApifyClientVersion.ClientVersion` — the semantic version of this library.
- `Apify.Client.ApifyClientVersion.ApiSpecVersion` — the Apify OpenAPI spec version this client was
  built against.

```csharp
using Apify.Client;

Console.WriteLine($"{ApifyClientVersion.ClientVersion} / {ApifyClientVersion.ApiSpecVersion}");
```

## License

[Apache-2.0](../LICENSE).
