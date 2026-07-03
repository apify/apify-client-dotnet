using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class WebhookIntegrationTests : IntegrationTestBase
{
    private static object WebhookDef(string url) => new
    {
        isAdHoc = true,
        eventTypes = new[] { "ACTOR.RUN.SUCCEEDED" },
        condition = new { actorRunId = "ZZZZZZZZZZZZZZZZZ" },
        requestUrl = url,
    };

    [SkippableFact]
    public async Task ListWebhooks()
    {
        var client = RequireClient();
        var page = await client.Webhooks().ListAsync(new ListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task ListWebhookDispatches()
    {
        var client = RequireClient();
        var page = await client.WebhookDispatches().ListAsync(new ListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task GetWebhook()
    {
        var client = RequireClient();
        var wh = await client.Webhooks().CreateAsync(WebhookDef("https://example.com/webhook"));
        try
        {
            var got = await client.Webhook(wh.Id!).GetAsync();
            Assert.NotNull(got);
            Assert.Equal(wh.Id, got!.Id);
        }
        finally
        {
            await client.Webhook(wh.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task GetWebhookDispatch()
    {
        var client = RequireClient();
        var wh = await client.Webhooks().CreateAsync(WebhookDef("https://example.com/webhook"));
        try
        {
            var dispatch = await client.Webhook(wh.Id!).TestAsync();
            var got = await client.WebhookDispatch(dispatch.Id!).GetAsync();
            Assert.NotNull(got);
            Assert.Equal(dispatch.Id, got!.Id);
        }
        finally
        {
            await client.Webhook(wh.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task WebhookCrudFlow()
    {
        var client = RequireClient();
        var wh = await client.Webhooks().CreateAsync(WebhookDef("https://example.com/webhook"));
        try
        {
            var webhook = client.Webhook(wh.Id!);
            Assert.NotNull(await webhook.GetAsync());
            var updated = await webhook.UpdateAsync(new { requestUrl = "https://example.com/updated" });
            Assert.Equal("https://example.com/updated", updated.RequestUrl);
            await webhook.Dispatches().ListAsync(new ListOptions());
            await webhook.TestAsync();
        }
        finally
        {
            await client.Webhook(wh.Id!).DeleteAsync();
        }
    }
}
