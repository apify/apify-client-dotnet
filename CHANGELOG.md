# Changelog

## 0.1.2

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-07-08T143931Z` and the
  project version to `0.1.2`.
- Aligned the `User-Agent` OS token with the other Apify clients: it now uses the short, lowercase
  platform identifier (`win32`, `darwin`, `linux`, `android`, `freebsd`) instead of `windows`.
- Request bodies of at least 1024 bytes are now compressed with brotli (`Content-Encoding: br`) before
  sending. Brotli is always used because .NET's `BrotliStream` is always available, so the reference
  client's gzip fallback (for runtimes lacking brotli) is not needed.

## 0.1.1

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-07-07T132551Z` and the
  project version to `0.1.1`.
- Corrected a wrong `LastRunOptions` doc comment: `Origin` and `Status` are spec-declared query
  parameters on the last-run endpoints (the comment had claimed `Origin` was not); `waitForFinish` is
  intentionally omitted for parity with the reference client's `lastRun`. Behaviour unchanged.

## 0.1.0

- Initial .NET client for the Apify API (spec `v2-2026-07-02T131926Z`).
- Resource clients for Actors, Actor versions and environment variables, builds, runs, datasets,
  key-value stores, request queues, tasks, schedules, webhooks, webhook dispatches, the Apify Store,
  users, and logs.
- Async-first API (`Task`-returning, `CancellationToken`-aware) with convenience helpers consistent
  with the JS reference client: `Actor().CallAsync()`/`StartAsync()`, `ValidateInputAsync()`,
  `DefaultBuildAsync()`, `LastRun()`, run `AbortAsync`/`MetamorphAsync`/`RebootAsync`/`ResurrectAsync`/
  `ChargeAsync`/`WaitForFinishAsync`, dataset `ListItemsAsync`/`DownloadItemsAsync`/`PushItemsAsync`/
  public URLs, key-value store records and public URLs, request queue batch add with retries, and log
  streaming.
- Auto-paging lazy iteration (`IAsyncEnumerable`) across all collection clients (`IterateAsync`) and
  dataset items (`IterateItemsAsync`), plus request-queue and Store iteration, matching the reference
  client's paginated iterators.
- Run log redirection: `RunClient.GetStreamedLog(toLog, fromStart)` returns a `StreamedLog` that forwards
  a run's live log to a sink one message at a time, and `Actor`/`Task` `CallAsync` accept a `log` sink that
  redirects the run's log for the duration of the wait.
- Last-run accessors forward their `status`/`origin` filters to the run's nested dataset, key-value store,
  request queue, and log clients.
- Binary-safe storage payloads: `KeyValueStoreRecord.Value` and `DownloadItemsAsync` return `byte[]`
  (raw bytes), and `SetRecordAsync` accepts `byte[]`, so binary records and exports (e.g. XLSX) are
  not corrupted; `SetRecordJsonAsync` serializes to JSON bytes.
- `RequestQueueRequest.UserData` and `ActorEnvVar` `Name`/`Value`/`IsSecret` omit the field when set to
  `null` (rather than writing a JSON `null`), honoring the documented null-omit contract.
- `BatchAddRequestsAsync` requires a non-empty `UniqueKey` per request, splits batches by both the
  25-request count limit and the ~9 MiB payload-size limit, dispatches chunks with up to
  `BatchAddRequestsOptions.MaxParallel` concurrent calls (results merged in input order), and retries
  only the requests the API reports unprocessed in a successful response. Consistent with the reference
  client, a failed batch call reports that chunk's not-yet-processed requests as unprocessed rather
  than throwing.
- `PaginationList<T>.Count` is the number of items in the page (matching the indexer); the total across
  all pages is exposed as `Total`.
- `Datasets().GetOrCreateAsync()` and `KeyValueStores().GetOrCreateAsync()` accept an optional schema.
- `RequestQueue(id, RequestQueueClientOptions)` accepts `ClientKey` and `TimeoutSecs`;
  `PaginateRequestsAsync()` accepts `PaginateRequestsOptions` (`Limit`, `MaxPageLimit`,
  `ExclusiveStartId`, `Cursor`, `Filter`).
- Replaceable HTTP transport (`IHttpTransport`) with a default `HttpClient`-based implementation;
  automatic retries with exponential backoff and jitter, growing per-attempt timeouts, and
  HMAC-SHA256 storage URL signing.
- Public `ApifyClientVersion.ClientVersion` and `ApifyClientVersion.ApiSpecVersion` constants.
- Integration test suite, documentation with runnable examples, a data-model property reference
  (`docs/models.md`), and CI workflows for integration tests and publishing (manual NuGet.org publish
  via Trusted Publishing: the `NuGet/login` action exchanges a GitHub OIDC token for a short-lived
  key, using only the `NUGET_USER` repository secret — no long-lived NuGet API key is stored).
