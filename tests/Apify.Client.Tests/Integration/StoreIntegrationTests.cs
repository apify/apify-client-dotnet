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
        await foreach (var item in client.Store().IterateAsync(new StoreListOptions { Limit = 5 }))
        {
            Assert.False(string.IsNullOrEmpty(item.Id));
            if (++count >= 12)
            {
                break;
            }
        }

        Assert.True(count >= 12, "expected to iterate at least 12 store actors");
    }
}
