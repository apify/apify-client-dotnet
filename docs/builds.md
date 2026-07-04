# Builds

Access the account-wide build collection with `client.Builds()`, an Actor's builds with
`client.Actor(id).Builds()`, and a specific build with `client.Build(buildId)`.

## Collection

- `ListAsync(ListOptions? options = null)` → `PaginationList<Build>` (`Offset`, `Limit`, `Desc`).
- `IterateAsync(ListOptions? options = null)` → `IAsyncEnumerable<Build>` (lazy, all pages).

## Single build — `client.Build(buildId)`

- `GetAsync(int? waitForFinishSecs = null)` → `Build?` — optionally waits up to `waitForFinishSecs`
  (server-side, max 60) for the build to finish.
- `AbortAsync()` → `Build`.
- `DeleteAsync()`.
- `WaitForFinishAsync(int? waitSecs = null)` → `Build` — client-side polling until terminal (`null`
  waits indefinitely).
- `GetOpenApiDefinitionAsync()` → `JsonObject?`.
- `Log()` → `LogClient`.

```csharp
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var build = await client.Actor("me/my-actor").BuildAsync("0.0", new ActorBuildOptions());
var finished = await client.Build(build.Id!).WaitForFinishAsync(300);
Console.WriteLine(finished.Status);

var log = await client.Build(build.Id!).Log().GetAsync();
Console.WriteLine(log);
```
