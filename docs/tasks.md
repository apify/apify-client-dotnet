# Tasks

Tasks are pre-configured Actor runs with stored input. Access the collection with `client.Tasks()` and
a specific task with `client.Task(id)`.

## Collection

- `ListAsync(ListOptions?)` → `PaginationList<ActorTask>`; `IterateAsync(ListOptions?)` →
  `IAsyncEnumerable<ActorTask>` (lazy, all pages).
- `CreateAsync(object task)` → `ActorTask`.

## Single task — `client.Task(id)`

- `GetAsync()`, `UpdateAsync(newFields)`, `DeleteAsync()`.
- `StartAsync(object? input = null, TaskStartOptions? options = null)` → `ActorRun`.
- `CallAsync(object? input = null, TaskStartOptions? options = null, int? waitSecs = null, Action<string>? log = null)`
  → `ActorRun` (`log`, if set, redirects the run's live log to that sink for the duration of the wait).
- `GetInputAsync()` / `UpdateInputAsync(object input)`.
- `LastRun(LastRunOptions?)` → `RunClient`; `Runs()` → `RunCollectionClient`.
- `Webhooks()` → read-only `NestedWebhookCollectionClient`.

The model is named `ActorTask` (not `Task`) to avoid colliding with `System.Threading.Tasks.Task`.

```csharp
using System;
using Apify.Client;

var client = new ApifyClient("my-api-token");
var task = await client.Tasks().CreateAsync(new
{
    actId = "apify/hello-world",
    name = "my-task",
    input = new { message = "hello" },
});

await client.Task(task.Id!).UpdateInputAsync(new { message = "updated" });
var run = await client.Task(task.Id!).CallAsync(null, null, 120);
Console.WriteLine(run.Status);
```
