# Changelog

## 0.4.0

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-10-01T153946Z` and
  the project version to `0.4.0`.
- Added `Build.ImageDigest`, matching the build's new `imageDigest` field in the OpenAPI spec.
- Dropped the now-removed "contact support" sentence from `TaskClient.PublishAsync`'s remarks and
  `docs/tasks.md`, matching the corrected spec wording.
- **Breaking:** added per-status `ApifyApiException` subclasses (`InvalidRequestException` 400,
  `UnauthorizedException` 401, `ForbiddenException` 403, `NotFoundException` 404, `ConflictException`
  409, `RateLimitException` 429, `ServerException` 5xx), matching the reference client's `ApifyApiError`
  subclass hierarchy. Every subclass still satisfies `catch (ApifyApiException)`.
- **Breaking:** a 404 on a resource addressed without its own id — `RunClient.Dataset()`,
  `.KeyValueStore()`, `.RequestQueue()`, `.Log()`, and `BuildClient.Log()` — now throws instead of
  resolving to `null`/no-op, since the ambiguity (the run/build itself, or the nested resource, could
  be missing) made the old `null` misleading. A record/request/log looked up by its own key or id
  (`GetRecordAsync`, `GetRequestAsync`, `client.Log(id)`) is unaffected and still resolves `null` for
  a missing one. Matches the reference client's `catchNotFoundForResourceOrThrow()`.
- **Breaking:** `DatasetClient.GetStatisticsAsync`, `ScheduleClient.GetLogAsync` and
  `TaskClient.GetInputAsync` now throw instead of resolving `null` when their parent resource does not
  exist (return types changed from `JsonObject?`/`string?` to `JsonObject`/`string`), matching the
  reference client.
- **Breaking:** `LogClient.StreamAsync` now returns `Stream?` (`null` for a missing log addressed by
  its own id, matching `GetAsync`) instead of always throwing, and still throws for a run/build-nested
  log client, matching the reference client's corrected behavior.
- Fixed `DatasetClient.IterateItemsAsync` to paginate by the API-reported scanned row count
  (`X-Apify-Pagination-Count`, falling back to the returned item count when the header is absent)
  instead of the number of items a page returned, so a fully filtered page (`Clean`/`SkipEmpty`/
  `SkipHidden`) no longer stalls or re-yields items, and `Unwind` returning more items than rows
  scanned no longer skips rows. Matches a bug fix in the reference client.
- Added a `format` parameter to `DatasetClient.CreateItemsPublicUrlAsync`, matching the reference
  client.
- Added request-body compression skip-list for already-compressed content types (images, audio,
  video, archives, office/ZIP formats, web fonts), matching the reference client's
  `isCompressibleContentType()`; a raw format under such a type (e.g. `image/bmp`) or a `+json`/`+xml`
  suffix (e.g. `image/svg+xml`) still compresses.
- `ApifyClient`'s `BaseUrl`/`PublicBaseUrl` now accept a URL that already ends with the `/v2` API
  version path without doubling it into `.../v2/v2`, matching the reference client.
- `ActorClient.StartAsync`/`ValidateInputAsync` and `RunClient.MetamorphAsync` now accept a `byte[]`
  input, sent as raw bytes instead of being JSON-encoded, matching the reference client's `ActorInput`
  (a plain object/array, or raw bytes). `TaskClient.StartAsync`'s input stays JSON-only, matching the
  reference client's `TaskStartOptions` (which has no content type to pair raw bytes with).
- **Breaking:** `ActorClient.Version`, `ActorVersionClient.EnvVar` and `ApifyClient.Build` now reject
  an empty id/name/version number instead of silently addressing the collection endpoint, matching the
  reference client.
- **Breaking:** added timeout tiers, matching the reference client's `TimeoutTier`. Every method that
  sends a request now internally picks `Short` (metadata reads/writes), `Medium` (listing/batch/trigger
  calls) or `Long` (downloads/uploads/streaming) instead of a single overall budget for every call.
  Added `ApifyClientOptions.TimeoutShortSecs` (default 5) and `TimeoutMediumSecs` (default 30);
  `TimeoutSecs` (default 360, unchanged) is now also documented as the `Long` tier's duration and the
  cap every attempt's growing timeout is clamped to regardless of tier. A method whose connection can be
  held open server-side for a `waitForFinish`-style wait (`BuildClient.GetAsync`, `RunClient.GetAsync`,
  `ActorClient.DefaultBuildAsync`/`StartAsync`, `TaskClient.StartAsync`) uses `Long` unconditionally so
  the HTTP timeout always covers the requested wait, rather than the reference's dynamic
  short-or-medium-extended-to-cover-the-wait. `RequestQueueClientOptions.TimeoutSecs` and
  `SetRecordOptions.TimeoutSecs` are unaffected: both are absolute per-call/per-client overrides that
  already took priority over any default and still do.
- Response URL fields the OpenAPI specification marks `format: uri` (currently `ActorRun.ContainerUrl`,
  `Webhook.RequestUrl` — the client's only two typed properties among the reference client's 17
  normalized fields) are now normalized to their RFC 3986 absolute-URI form via `System.Uri`
  (lowercased/punycoded host, default port dropped, empty path becomes `/`, unsafe characters
  percent-encoded), matching the reference client's `z.url({ normalize: true })`. A present but
  invalid absolute URL now throws `FormatException` from the property getter.
- **Breaking:** `ActorVersionCollectionClient.ListAsync`/`IterateAsync` no longer take a `ListOptions`
  parameter: the endpoint returns every version in one response and never read `Offset`/`Limit`/`Desc`,
  matching the reference client.

## 0.3.5

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-09-28T115051Z` and
  the project version to `0.3.5`.
- Updated the task-publishing limit documented on `TaskClient.PublishAsync` and in `docs/tasks.md`
  from "fewer than 50 published tasks" to "not already have 10 published tasks (100 per account)",
  matching the corrected spec wording.

## 0.3.4

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-09-10T091137Z` and
  the project version to `0.3.4`. This spec update only documents `X-Apify-Pagination-*` response
  headers and the `offset`/`limit`/`desc` query parameters already supported by this client; no
  client behavior changed.
- Added missing iteration integration tests (`IterationIntegrationTests`) covering every
  `IterateAsync` collection client that previously had no dedicated test: Actors, Actor versions,
  Actor environment variables, datasets, key-value stores, request queues, tasks, schedules,
  webhooks, builds, runs, and webhook dispatches.

## 0.3.3

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-09-02T154542Z` and
  the project version to `0.3.3`.
- Added `ActorTask.Description`, matching the task's new `description` field in the OpenAPI spec.

## 0.3.2

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-08-27T071624Z` and
  the project version to `0.3.2`.

## 0.3.1

- Bumped `ApifyClientVersion.ApiSpecVersion` to the Apify OpenAPI spec `v2-2026-08-14T072928Z`.
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
