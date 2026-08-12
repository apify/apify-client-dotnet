# Data models

The resource clients return strongly-typed models from the `Apify.Client.Models` namespace. Most
models are thin, read-only wrappers over the API's JSON response: every documented field is exposed as
a property, and the underlying JSON is always available via `ToJsonObject()` (declared on the shared
`ApifyResource` base class) for fields not surfaced as first-class properties.

```csharp
using System;
using Apify.Client;

var client = new ApifyClient("my-api-token");
var run = await client.Actor("apify/hello-world").CallAsync(null, null, 120);

Console.WriteLine(run.Status);              // typed property
Console.WriteLine(run.ToJsonObject()["id"]); // raw JSON escape hatch
```

Reference-typed properties are nullable (`string?`, `bool?`, …) because the API omits fields that do
not apply to a given resource; treat `null` as "not present".

## `ApifyResource` (base class)

Base class for every JSON-backed model below.

| Member | Type | Description |
|---|---|---|
| `ToJsonObject()` | `JsonObject` | The raw underlying JSON object, for reading fields not exposed as typed properties. |
| `Get(string key)` | `JsonNode?` | The raw JSON value for a single key, or `null` if absent. |

## `Actor`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The Actor's unique ID. |
| `UserId` | `string?` | ID of the user who owns the Actor. |
| `Name` | `string?` | The Actor's technical name. |
| `Username` | `string?` | Username of the Actor's owner. |
| `Title` | `string?` | Human-readable title. |
| `Description` | `string?` | Free-text description. |
| `IsPublic` | `bool?` | Whether the Actor is published publicly in the Apify Store. |
| `CreatedAt` | `string?` | ISO 8601 creation timestamp. |
| `ModifiedAt` | `string?` | ISO 8601 last-modification timestamp. |

## `ActorRun`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The run's unique ID. |
| `ActId` | `string?` | ID of the Actor that was run. |
| `ActorTaskId` | `string?` | ID of the task the run originated from, if any. |
| `UserId` | `string?` | ID of the user who started the run. |
| `Status` | `string?` | Run status (e.g. `RUNNING`, `SUCCEEDED`, `FAILED`, `ABORTED`). |
| `StatusMessage` | `string?` | Human-readable status message. |
| `StartedAt` | `string?` | ISO 8601 start timestamp. |
| `FinishedAt` | `string?` | ISO 8601 finish timestamp (`null` while running). |
| `BuildId` | `string?` | ID of the Actor build used for the run. |
| `DefaultDatasetId` | `string?` | ID of the run's default dataset. |
| `DefaultKeyValueStoreId` | `string?` | ID of the run's default key-value store. |
| `DefaultRequestQueueId` | `string?` | ID of the run's default request queue. |
| `ContainerUrl` | `string?` | URL of the run's container (for live access while running). |
| `IsTerminal` | `bool` | `true` if `Status` is a terminal state (succeeded/failed/aborted/timed-out). |

## `Build`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The build's unique ID. |
| `ActId` | `string?` | ID of the Actor that was built. |
| `Status` | `string?` | Build status (e.g. `RUNNING`, `SUCCEEDED`, `FAILED`). |
| `StartedAt` | `string?` | ISO 8601 start timestamp. |
| `FinishedAt` | `string?` | ISO 8601 finish timestamp (`null` while building). |
| `BuildNumber` | `string?` | The semantic build number. |
| `IsTerminal` | `bool` | `true` if `Status` is a terminal state. |

## `ActorVersion`

| Property | Type | Description |
|---|---|---|
| `VersionNumber` | `string?` | The version's number (e.g. `0.1`). |
| `SourceType` | `string?` | How the source is provided (e.g. `SOURCE_FILES`, `GIT_REPO`, `TARBALL`, `GITHUB_GIST`). |

## `ActorEnvVar`

A read/write model (used both as request input and response). Fields set to `null` are omitted from
the request JSON.

| Member | Type | Description |
|---|---|---|
| `ActorEnvVar(string? name = null, string? value = null, bool? isSecret = null)` | constructor | Build an environment variable to create/update. |
| `Name` | `string?` | The variable name. |
| `Value` | `string?` | The variable value. |
| `IsSecret` | `bool?` | Whether the value is stored encrypted and hidden. |

## `ActorStoreListItem`

An entry returned when browsing the Apify Store.

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The Actor's unique ID. |
| `Name` | `string?` | The Actor's technical name. |
| `Username` | `string?` | Username of the Actor's owner. |
| `Title` | `string?` | Human-readable title. |

## `ActorTask`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The task's unique ID. |
| `ActId` | `string?` | ID of the Actor the task runs. |
| `UserId` | `string?` | ID of the user who owns the task. |
| `Name` | `string?` | The task's technical name. |
| `Title` | `string?` | Human-readable title. |
| `CreatedAt` | `string?` | ISO 8601 creation timestamp. |
| `ModifiedAt` | `string?` | ISO 8601 last-modification timestamp. |
| `IsPublic` | `bool?` | Whether the task is published on its public landing page. Set via `TaskClient.PublishAsync()`/`UnpublishAsync()`, not directly. |
| `PublicConfig` | `TaskPublicConfig?` | The task's public landing page display configuration, or `null` if not set. |

## `TaskPublicConfig`

| Property | Type | Description |
|---|---|---|
| `PublishedAt` | `string?` | ISO 8601 timestamp the task was published, or `null` if unpublished. Read-only. |
| `SeoTitle` | `string?` | Name to display for search engines. |
| `SeoDescription` | `string?` | Description to display for search engines. |
| `Categorization` | `string?` | The task's category on its public landing page. |
| `InputSchemaFields` | `IReadOnlyList<string>?` | Input schema fields shown on the public landing page. |
| `DatasetName` | `string?` | Name of the dataset shown on the public landing page. |
| `DatasetView` | `string?` | View of the dataset shown on the public landing page. |

## `Dataset`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The dataset's unique ID. |
| `Name` | `string?` | The dataset's name (`null` for unnamed datasets). |
| `UserId` | `string?` | ID of the owning user. |
| `CreatedAt` | `string?` | ISO 8601 creation timestamp. |
| `ModifiedAt` | `string?` | ISO 8601 last-modification timestamp. |
| `ItemCount` | `long?` | Number of items stored in the dataset. |

## `KeyValueStore`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The store's unique ID. |
| `Name` | `string?` | The store's name (`null` for unnamed stores). |
| `UserId` | `string?` | ID of the owning user. |
| `CreatedAt` | `string?` | ISO 8601 creation timestamp. |
| `ModifiedAt` | `string?` | ISO 8601 last-modification timestamp. |

## `KeyValueStoreRecord`

The value of a single key-value store record. `Value` holds the raw bytes so binary records (images,
XLSX exports, …) are returned intact.

| Property | Type | Description |
|---|---|---|
| `Key` | `string` | The record's key. |
| `Value` | `byte[]` | The raw record bytes. |
| `ContentType` | `string?` | The record's MIME type (e.g. `application/json`). |

## `KeyValueStoreKey`

| Property | Type | Description |
|---|---|---|
| `Key` | `string?` | The record key. |
| `Size` | `long?` | Size of the record's value in bytes. |

## `KeyValueStoreKeysPage`

One page of key listings (returned by `ListKeysAsync`).

| Property | Type | Description |
|---|---|---|
| `Items` | `IReadOnlyList<KeyValueStoreKey>` | The keys in this page. |
| `Limit` | `long` | The page-size limit that was applied. |
| `IsTruncated` | `bool` | `true` if more keys exist beyond this page. |
| `ExclusiveStartKey` | `string?` | The exclusive start key this page began after. |
| `NextExclusiveStartKey` | `string?` | Start key to pass to fetch the next page. |

## `RequestQueue`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The queue's unique ID. |
| `Name` | `string?` | The queue's name (`null` for unnamed queues). |
| `UserId` | `string?` | ID of the owning user. |
| `CreatedAt` | `string?` | ISO 8601 creation timestamp. |
| `ModifiedAt` | `string?` | ISO 8601 last-modification timestamp. |
| `TotalRequestCount` | `long?` | Total number of requests ever added to the queue. |

## `RequestQueueRequest`

A read/write model (request input and response). Fields set to `null` are omitted from request JSON.

| Member | Type | Description |
|---|---|---|
| `RequestQueueRequest(string? url = null, string? uniqueKey = null)` | constructor | Build a request to add to a queue. |
| `Id` | `string?` | The request's unique ID (assigned by the queue). |
| `Url` | `string?` | The request URL. |
| `UniqueKey` | `string?` | The key used to deduplicate the request within the queue. |
| `Method` | `string?` | HTTP method (defaults to `GET` on the server). |
| `UserData` | `JsonNode?` | Arbitrary user-defined JSON payload attached to the request. |
| `RetryCount` | `long?` | Number of times the request has been retried (assigned by the queue). |
| `LockExpiresAt` | `string?` | ISO 8601 lock expiry; only set on requests returned by `ListAndLockHeadAsync`. |

## `RequestQueueHead`

The head (front) of a request queue.

| Property | Type | Description |
|---|---|---|
| `Items` | `IReadOnlyList<RequestQueueRequest>` | The requests at the head of the queue. |
| `Limit` | `long` | The page-size limit that was applied. |
| `QueueModifiedAt` | `string?` | ISO 8601 timestamp of the last modification to the queue. |
| `HadMultipleClients` | `bool` | `true` if more than one client has accessed the queue (concurrency hint). |

## `LockedRequestQueueHead`

The result of `RequestQueueClient.ListAndLockHeadAsync()`: a batch of requests locked for exclusive
processing. Each item's `RequestQueueRequest.LockExpiresAt` holds its individual lock expiry.

| Property | Type | Description |
|---|---|---|
| `Items` | `IReadOnlyList<RequestQueueRequest>` | The locked requests. |
| `Limit` | `long` | The maximum number of requests requested. |
| `QueueModifiedAt` | `string?` | ISO 8601 timestamp of the last modification to the queue. |
| `HadMultipleClients` | `bool` | `true` if more than one client has accessed the queue. |
| `LockSecs` | `long` | The lock duration applied to every returned request, in seconds. |
| `QueueHasLockedRequests` | `bool?` | Whether the queue has any requests locked by any client. |
| `ClientKey` | `string?` | The client key used to acquire the locks. |

## `RequestLockInfo`

The result of `RequestQueueClient.ProlongRequestLockAsync()`.

| Property | Type | Description |
|---|---|---|
| `LockExpiresAt` | `string?` | ISO 8601 timestamp the (possibly just-extended) lock expires at. |

## `UnlockRequestsResult`

The result of `RequestQueueClient.UnlockRequestsAsync()`.

| Property | Type | Description |
|---|---|---|
| `UnlockedCount` | `long` | Number of requests that were unlocked. |

## `RequestQueueRequestsPage`

One cursor-paginated page of `RequestQueueClient.ListRequestsAsync()`.

| Property | Type | Description |
|---|---|---|
| `Items` | `IReadOnlyList<RequestQueueRequest>` | The requests in this page. |
| `Limit` | `long` | The page-size limit that was applied. |
| `ExclusiveStartId` | `string?` | Deprecated by the API in favor of `Cursor`/`NextCursor`. |
| `Cursor` | `string?` | The cursor that produced this page. |
| `NextCursor` | `string?` | Cursor to pass to fetch the next page, or `null` if this is the last page. |

## `BatchDeleteResult`

The aggregate result of `RequestQueueClient.BatchDeleteRequestsAsync()`.

| Property | Type | Description |
|---|---|---|
| `ProcessedRequests` | `IReadOnlyList<RequestQueueRequest>` | Requests successfully deleted. |
| `UnprocessedRequests` | `IReadOnlyList<RequestQueueRequest>` | Requests that failed to delete and can be retried. |

## `RequestQueueOperationInfo`

The result of adding/updating a single request.

| Property | Type | Description |
|---|---|---|
| `RequestId` | `string?` | ID of the affected request. |
| `UniqueKey` | `string?` | The request's unique key. |
| `WasAlreadyPresent` | `bool?` | `true` if a request with the same unique key already existed. |
| `WasAlreadyHandled` | `bool?` | `true` if that existing request was already marked handled. |

## `BatchAddResult`

The aggregate result of a batch add-requests operation.

| Property | Type | Description |
|---|---|---|
| `ProcessedRequests` | `IReadOnlyList<RequestQueueOperationInfo>` | Requests the API accepted. |
| `UnprocessedRequests` | `IReadOnlyList<RequestQueueRequest>` | Requests that could not be processed (after retries). |

## `Schedule`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The schedule's unique ID. |
| `UserId` | `string?` | ID of the owning user. |
| `Name` | `string?` | The schedule's name. |
| `CronExpression` | `string?` | The cron expression controlling when it fires. |
| `IsEnabled` | `bool?` | Whether the schedule is currently enabled. |

## `Webhook`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The webhook's unique ID. |
| `UserId` | `string?` | ID of the owning user. |
| `RequestUrl` | `string?` | URL the webhook posts to when triggered. |
| `EventTypes` | `IReadOnlyList<string>?` | The event types that trigger the webhook. |

## `WebhookDispatch`

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The dispatch's unique ID. |
| `WebhookId` | `string?` | ID of the webhook that was dispatched. |

## `User`

Returned by `client.Me().GetAsync()` (private account details) and `client.User(id).GetAsync()`
(public profile). Only the always-present fields are typed; read the rest via `ToJsonObject()`.

| Property | Type | Description |
|---|---|---|
| `Id` | `string?` | The user's unique ID. |
| `Username` | `string?` | The user's username. |

## `PaginationList<T>`

One page of a paginated listing, returned by every `ListAsync` method.

| Property | Type | Description |
|---|---|---|
| `Items` | `IReadOnlyList<T>` | The items on this page. |
| `Count` | `long` | Number of items on this page (equals `Items.Count`). |
| `Total` | `long` | Total number of items across all pages. |
| `Offset` | `long` | The offset this page started at. |
| `Limit` | `long` | The page-size limit that was applied. |
| `Desc` | `bool` | Whether the listing is in descending order. |

To iterate every item across pages without managing offsets yourself, use the matching
`IterateAsync` method (an `IAsyncEnumerable<T>`) instead — see the resource-specific docs.
