# Changelog

## 0.3.1

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-08-14T072928Z`,
  which officially documents the `Task.isPublic`/`publicConfig` fields and `TaskPublicConfig`
  schema this client already implemented ahead of the spec (0.2.0).
- Corrected the publish/unpublish permission wording in `TaskClient.PublishAsync`/`UnpublishAsync`,
  `docs/tasks.md`, and the `TaskPublishUnpublish` integration test comment: publishing/unpublishing
  requires write permission to the task's Actor only, not to the task itself.
- `TaskClient.PublishAsync`'s doc comment and `docs/tasks.md` now state the concrete publish
  preconditions from the spec (Actor public, `publicConfig.inputSchemaFields` and
  `publicConfig.datasetView` set, Actor has fewer than 50 published tasks).
- Noted in `TaskPublicConfig.Categorization`'s doc comment and `docs/models.md` that the field is
  not part of the documented schema and is kept only for parity with the reference JS client.

## 0.3.0

Breaking: `RequestQueueClient` methods that previously returned a raw `JsonObject`/took an untyped
`object` now use typed models, matching the OpenAPI-documented response schemas and the typing the
sibling clients already apply to this same resource:

- `ListAndLockHeadAsync` now returns `LockedRequestQueueHead` (was `JsonObject`).
- `ProlongRequestLockAsync` now returns `RequestLockInfo` (was `JsonObject`).
- `UnlockRequestsAsync` now returns `UnlockRequestsResult` (was `JsonObject`).
- `ListRequestsAsync` now returns `RequestQueueRequestsPage` (was `JsonObject`).
- `BatchDeleteRequestsAsync` now takes `IReadOnlyList<RequestQueueRequest>` and returns
  `BatchDeleteResult` (was `object requests` / `JsonObject`).
- `RequestQueueRequest` gained `RetryCount`/`LockExpiresAt` properties, populated on requests returned
  by `ListAndLockHeadAsync`/`ListRequestsAsync`.
- `RequestQueueHead` and `LockedRequestQueueHead` gained the previously-missing `QueueModifiedAt`
  field (present in the OpenAPI spec and the reference client, but not yet exposed by this client).

## 0.2.0

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-08-05T133145Z` and the
  project version to `0.2.0`.
- Added `TaskClient.PublishAsync()` and `TaskClient.UnpublishAsync()`, plus `ActorTask.IsPublic` and
  `ActorTask.PublicConfig` (backed by the new `TaskPublicConfig` model), matching the reference
  client's task publish/unpublish support.
- Removed the duplicated AI-disclaimer paragraph from `docs/README.md` and the `ApifyClient` XML
  doc comment; it is now stated once, in the top-level `README.md`, per the client requirements.

## 0.1.4

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-07-13T092445Z` and the
  project version to `0.1.4`.

## 0.1.3

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-07-10T105921Z` and the
  project version to `0.1.3`.
- The default HTTP transport now negotiates and transparently decompresses brotli, gzip and deflate
  responses, matching the API's newly documented response compression and the reference client.

## 0.1.2

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-07-08T143931Z` and the
  project version to `0.1.2`.
- Aligned the `User-Agent` OS token with the reference client's Node `os.platform()` values (`win32`,
  `darwin`, `linux`, `android`, `freebsd`, and — for platforms without a dedicated .NET helper —
  `openbsd`, `netbsd`, `sunos`, `aix`) instead of `windows`.
- Request bodies of at least 1024 bytes are compressed before sending. The compression algorithm is
  selectable via the new `ApifyClientOptions.RequestCompression` option (`RequestCompression` enum):
  brotli (`Content-Encoding: br`) by default, or gzip (`Content-Encoding: gzip`).

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
