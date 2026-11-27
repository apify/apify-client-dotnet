using System;
using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class StoreIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task ListStore()
    {
        var client = RequireClient();
        var page = await client.Store().ListAsync(new StoreListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
    }

    [SkippableFact]
    public async Task IterateStore()
    {
        var client = RequireClient();
        var count = 0;
        await foreach (var item in client.Store().IterateAsync(new StoreListOptions { ChunkSize = 5 }))
        {
            Assert.False(string.IsNullOrEmpty(item.Id));
            if (++count >= 12)
            {
                break;
            }
        }

        Assert.True(count >= 12, "expected to iterate at least 12 store actors");
    }

    [SkippableFact]
    public async Task IterateStoreStopsAtLimit()
    {
        var client = RequireClient();
        var count = 0;
        await foreach (var unused in client.Store().IterateAsync(new StoreListOptions { Limit = 7, ChunkSize = 3 }))
        {
            _ = unused;
            count++;
        }

        Assert.Equal(7, count);
    }

    [SkippableFact]
    public async Task ListStoreWithInputSchema()
    {
        var client = RequireClient();
        var page = await client.Store().ListAsync(new StoreListOptions { IncludeInputSchema = true, Limit = StoreListOptions.MaxPageLimitWithInputSchema });

        Assert.NotEmpty(page.Items);
        Assert.Contains(page.Items, static item => item.Get("inputSchema") is not null);
    }

    [SkippableFact]
    public async Task ListStoreExposesTheStoreStats()
    {
        var client = RequireClient();
        var page = await client.Store().ListAsync(new StoreListOptions { Limit = 5 });

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, static item =>
        {
            Assert.NotNull(item.Stats);
            Assert.NotNull(item.Stats["totalUsers"]);
            Assert.NotNull(item.Stats["actorReviewCount"]);
        });
        Assert.Contains(page.Items, static item => !string.IsNullOrEmpty(item.UserFullName));
        Assert.Contains(page.Items, static item => !string.IsNullOrEmpty(item.Description));
        Assert.Contains(page.Items, static item => item.IsVerified is not null);
    }

    [SkippableFact]
    public async Task ListStoreByCategory()
    {
        var client = RequireClient();
        var page = await client.Store().ListAsync(new StoreListOptions { Category = "AI_AND_ML", Limit = 5 });

        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, static item => Assert.Contains("AI_AND_ML", item.Categories ?? Array.Empty<string>()));
    }

    [SkippableFact]
    public async Task SearchStoreReturnsASinglePage()
    {
        var client = RequireClient();
        var page = await client.Store().ListAsync(new StoreListOptions { Search = "web scraper" });
        Assert.NotEmpty(page.Items);
        Assert.True(page.Items.Count <= 100);

        var iterated = 0;
        await foreach (var unused in client.Store().IterateAsync(new StoreListOptions { Search = "web scraper" }))
        {
            _ = unused;
            iterated++;
        }

        Assert.Equal(page.Items.Count, iterated);
    }

    [SkippableFact]
    public async Task IterateStoreSearchIsBoundedByLimitNotChunkSize()
    {
        var client = RequireClient();
        var count = 0;
        await foreach (var unused in client.Store().IterateAsync(new StoreListOptions { Search = "web scraper", Limit = 30, ChunkSize = 10 }))
        {
            _ = unused;
            count++;
        }

        Assert.Equal(30, count);
    }

    [SkippableFact]
    public async Task IterateStoreSearchWithInputSchemaIsCappedAtTheApiMaximum()
    {
        var client = RequireClient();
        var count = 0;
        await foreach (var item in client.Store().IterateAsync(new StoreListOptions { Search = "web scraper", IncludeInputSchema = true, Limit = 50, ChunkSize = 10 }))
        {
            Assert.NotNull(item.Get("inputSchema"));
            count++;
        }

        Assert.Equal(StoreListOptions.MaxPageLimitWithInputSchema, count);
    }

    [SkippableFact]
    public async Task StoreAcceptsTheDeprecatedSortBy()
    {
        var client = RequireClient();
#pragma warning disable CS0618 // The API still accepts the deprecated option, so the client has to keep forwarding it.
        var page = await client.Store().ListAsync(new StoreListOptions { SortBy = "popularity", Limit = 5 });
#pragma warning restore CS0618

        Assert.NotEmpty(page.Items);
    }
}
