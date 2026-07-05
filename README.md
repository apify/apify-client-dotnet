# Apify API client for .NET

> **Official, but experimental — AI-generated and AI-maintained.** This is an official Apify client,
> but it is experimental: it is generated and maintained by AI. Review the code before relying on it in
> production and report issues on the repository.

A resource-oriented .NET client for the [Apify API](https://docs.apify.com/api/v2), mirroring the
official [JavaScript](https://github.com/apify/apify-client-js) reference client: start from an
`ApifyClient`, then drill down into resources (Actors, runs, datasets, key-value stores, request
queues, tasks, schedules, webhooks, the store, users and logs).

All calls are asynchronous (`Task`-returning, `CancellationToken`-aware).

## Requirements

- .NET 8.0 or newer.

## Installation

```bash
dotnet add package Apify.Client
```

## Quick start

`ImplicitUsings` is disabled in this repository, so every `using` (even `System`) is listed explicitly.

```csharp
using System;
using Apify.Client;

var client = new ApifyClient("my-api-token");

// Start an Actor and wait for it to finish (null waits indefinitely; pass seconds to bound the wait).
var run = await client.Actor("apify/hello-world").CallAsync(null, null, null);

// Read items from the run's default dataset.
var items = await client.Dataset(run.DefaultDatasetId!).ListItemsAsync();
Console.WriteLine("Item count: " + items.Count);
```

`new ApifyClient("my-api-token")` takes the token as an explicit argument — it does **not** read
`APIFY_TOKEN` automatically. Get your token from the
[Apify Console → Settings → API & Integrations](https://console.apify.com/settings/integrations).

## Documentation

Full documentation lives in [`docs/`](docs/README.md), organized by resource, with runnable
[examples](docs/examples.md).

## Versioning

- `Apify.Client.ApifyClientVersion.ClientVersion` — the semantic version of this library.
- `Apify.Client.ApifyClientVersion.ApiSpecVersion` — the Apify OpenAPI spec version this client was
  built against.

## License

[Apache-2.0](LICENSE).
