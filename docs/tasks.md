# Tasks

Tasks are pre-configured Actor runs with stored input. Access the collection with `client.Tasks()` and
a specific task with `client.Task(id)`.

## Collection

- `ListAsync(ListOptions?)` → `PaginationList<ActorTask>`.
- `CreateAsync(object task)` → `ActorTask`.

## Single task — `client.Task(id)`

- `GetAsync()`, `UpdateAsync(newFields)`, `DeleteAsync()`.
- `StartAsync(object? input = null, TaskStartOptions? options = null)` → `ActorRun`.
- `CallAsync(object? input = null, TaskStartOptions? options = null, int? waitSecs = null)` → `ActorRun`.
- `GetInputAsync()` / `UpdateInputAsync(object input)`.
- `LastRun(LastRunOptions?)` → `RunClient`; `Runs()` → `RunCollectionClient`.
- `Webhooks()` → read-only `NestedWebhookCollectionClient`.

The model is named `ActorTask` (not `Task`) to avoid colliding with `System.Threading.Tasks.Task`.

```csharp
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
