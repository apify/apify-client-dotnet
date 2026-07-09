# Webhooks

Webhooks notify an external service when specific events occur. Access the account-wide collection with
`client.Webhooks()` and a specific webhook with `client.Webhook(id)`. Webhook dispatches are read with
`client.WebhookDispatches()` and `client.WebhookDispatch(id)`.

## Webhook collection — `client.Webhooks()`

- `ListAsync(ListOptions? options = null)` → `PaginationList<Webhook>`;
  `IterateAsync(ListOptions? options = null)` → `IAsyncEnumerable<Webhook>` (lazy, all pages). Webhook
  dispatches expose the same pair.
- `CreateAsync(object webhook)` → `Webhook`.

Webhooks nested under an Actor or task (`client.Actor(id).Webhooks()`, `client.Task(id).Webhooks()`)
are **read-only** (list only); create account-wide webhooks that target an Actor/task via the webhook's
`condition`.

## Single webhook — `client.Webhook(id)`

- `GetAsync()`, `UpdateAsync(newFields)`, `DeleteAsync()`.
- `TestAsync()` → `WebhookDispatch` (dispatch immediately).
- `Dispatches()` → `WebhookDispatchCollectionClient`.

The webhook definition is an ordinary JSON-serializable object. Use `WebhookEventType.ToWireValue()` to
turn the strongly-typed event enum into the string the API expects; the `Webhook.EventTypes` you read
back is a typed `IReadOnlyList<WebhookEventType>`.

```csharp
using System;
using Apify.Client;
using Apify.Client.Models;
using Apify.Client.Options;

var client = new ApifyClient("my-api-token");
var webhook = await client.Webhooks().CreateAsync(new
{
    eventTypes = new[] { WebhookEventType.ActorRunSucceeded.ToWireValue() },
    condition = new { actorId = "apify/hello-world" },
    requestUrl = "https://example.com/webhook",
});

Console.WriteLine(webhook.EventTypes?.Contains(WebhookEventType.ActorRunSucceeded)); // True

await client.Webhook(webhook.Id!).UpdateAsync(new { requestUrl = "https://example.com/updated" });
await client.Webhook(webhook.Id!).Dispatches().ListAsync(new ListOptions { Limit = 10 });
```
