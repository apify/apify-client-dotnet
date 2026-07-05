# Schedules

Schedules automatically start Actor or task runs at specified times. Access the collection with
`client.Schedules()` and a specific schedule with `client.Schedule(id)`.

## Collection

- `ListAsync(ListOptions? options = null)` → `PaginationList<Schedule>`;
  `IterateAsync(ListOptions? options = null)` → `IAsyncEnumerable<Schedule>` (lazy, all pages).
- `CreateAsync(object schedule)` → `Schedule`.

## Single schedule — `client.Schedule(id)`

- `GetAsync()` → `Schedule?`; `UpdateAsync(object newFields)` → `Schedule`; `DeleteAsync()`.
- `GetLogAsync()` → `string?` (invocation log; `null` if none yet).

```csharp
using Apify.Client;

var client = new ApifyClient("my-api-token");
var schedule = await client.Schedules().CreateAsync(new
{
    name = "nightly",
    cronExpression = "0 0 * * *",
    isEnabled = true,
    actions = new object[] { new { type = "RUN_ACTOR", actorId = "apify/hello-world" } },
});

await client.Schedule(schedule.Id!).UpdateAsync(new { cronExpression = "0 12 * * *" });
```
