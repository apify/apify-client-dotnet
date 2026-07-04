# Webhooks

Webhooks notify an external service when specific events occur. Access the account-wide collection with
`client.Webhooks()` and a specific webhook with `client.Webhook(id)`. Webhook dispatches are read with
`client.WebhookDispatches()` and `client.WebhookDispatch(id)`.

## Webhook collection — `client.Webhooks()`

- `ListAsync(ListOptions?)` → `PaginationList<Webhook>`; `IterateAsync(ListOptions?)` →
  `IAsyncEnumerable<Webhook>` (lazy, all pages). Webhook dispatches expose the same pair.
- `CreateAsync(object webhook)` → `Webhook`.

Webhooks nested under an Actor or task (`client.Actor(id).Webhooks()`, `client.Task(id).Webhooks()`)
are **read-only** (list only); create account-wide webhooks that target an Actor/task via the webhook's
`condition`.

## Single webhook — `client.Webhook(id)`

- `GetAsync()`, `UpdateAsync(newFields)`, `DeleteAsync()`.
- `TestAsync()` → `WebhookDispatch` (dispatch immediately).
- `Dispatches()` → `WebhookDispatchCollectionClient`.

```csharp
using Apify.Client;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var webhook = await client.Webhooks().CreateAsync(new
{
    eventTypes = new[] { "ACTOR.RUN.SUCCEEDED" },
    condition = new { actorId = "apify/hello-world" },
    requestUrl = "https://example.com/webhook",
});

await client.Webhook(webhook.Id!).UpdateAsync(new { requestUrl = "https://example.com/updated" });
await client.Webhook(webhook.Id!).Dispatches().ListAsync(new ListOptions { Limit = 10 });
```
