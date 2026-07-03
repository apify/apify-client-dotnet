using System.Threading.Tasks;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class UserIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task GetOwnAccount()
    {
        var client = RequireClient();
        var user = await client.Me().GetAsync();
        Assert.NotNull(user);
        Assert.False(string.IsNullOrEmpty(user!.Id));
    }

    [SkippableFact]
    public async Task GetMonthlyUsage()
    {
        var client = RequireClient();
        Assert.NotEmpty(await client.Me().MonthlyUsageAsync());
    }

    [SkippableFact]
    public async Task GetMonthlyUsageForDate()
    {
        var client = RequireClient();
        Assert.NotEmpty(await client.Me().MonthlyUsageAsync("2026-06-01"));
    }

    [SkippableFact]
    public async Task GetLimits()
    {
        var client = RequireClient();
        Assert.NotEmpty(await client.Me().LimitsAsync());
    }
}
