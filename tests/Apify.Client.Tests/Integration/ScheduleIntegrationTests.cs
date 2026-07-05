using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class ScheduleIntegrationTests : IntegrationTestBase
{
    private static object ScheduleDef(string name) => new
    {
        name,
        cronExpression = "0 0 * * *",
        isEnabled = false,
        isExclusive = true,
        actions = System.Array.Empty<object>(),
    };

    [SkippableFact]
    public async Task ListSchedules()
    {
        var client = RequireClient();
        var page = await client.Schedules().ListAsync(new ListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task GetSchedule()
    {
        var client = RequireClient();
        var sch = await client.Schedules().CreateAsync(ScheduleDef(UniqueName("sch-get")));
        try
        {
            var got = await client.Schedule(sch.Id!).GetAsync();
            Assert.NotNull(got);
            Assert.Equal(sch.Id, got!.Id);
        }
        finally
        {
            await client.Schedule(sch.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task ScheduleCrudFlow()
    {
        var client = RequireClient();
        var sch = await client.Schedules().CreateAsync(ScheduleDef(UniqueName("sch-crud")));
        try
        {
            var schedule = client.Schedule(sch.Id!);
            Assert.NotNull(await schedule.GetAsync());
            var updated = await schedule.UpdateAsync(new { cronExpression = "0 12 * * *" });
            Assert.Equal("0 12 * * *", updated.CronExpression);
            // A fresh schedule may have no log yet (null), which is a valid result — we only assert the call succeeds.
            await schedule.GetLogAsync();
        }
        finally
        {
            await client.Schedule(sch.Id!).DeleteAsync();
        }
    }
}
