using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class TaskIntegrationTests : IntegrationTestBase
{
    private static object TaskDef(string name) => new
    {
        actId = "apify/hello-world",
        name,
        options = new { build = "latest", memoryMbytes = 256, timeoutSecs = 60 },
        input = new { message = "hello" },
    };

    [SkippableFact]
    public async Task ListTasks()
    {
        var client = RequireClient();
        var page = await client.Tasks().ListAsync(new ListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task GetTask()
    {
        var client = RequireClient();
        var task = await client.Tasks().CreateAsync(TaskDef(UniqueName("task-get")));
        try
        {
            var got = await client.Task(task.Id!).GetAsync();
            Assert.NotNull(got);
            Assert.Equal(task.Id, got!.Id);
        }
        finally
        {
            await client.Task(task.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task TaskCrudFlow()
    {
        var client = RequireClient();
        var task = await client.Tasks().CreateAsync(TaskDef(UniqueName("task-crud")));
        try
        {
            var tc = client.Task(task.Id!);
            Assert.NotNull(await tc.GetAsync());
            await tc.UpdateInputAsync(new { message = "updated" });
            Assert.NotNull(await tc.GetInputAsync());
            await tc.UpdateAsync(new { name = UniqueName("task-renamed") });
            await tc.Runs().ListAsync(new ListOptions(), new RunListOptions());
        }
        finally
        {
            await client.Task(task.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task TaskPublishUnpublish()
    {
        var client = RequireClient();
        var task = await client.Tasks().CreateAsync(TaskDef(UniqueName("task-publish")));
        try
        {
            var tc = client.Task(task.Id!);

            // Unpublishing an already-unpublished task is a documented no-op: it succeeds and returns
            // the task unchanged (still not public).
            var unpublished = await tc.UnpublishAsync();
            Assert.Equal(task.Id, unpublished.Id);
            Assert.True(unpublished.IsPublic != true);

            // Publishing requires write permission to both the task and its Actor (apify/hello-world),
            // which the test account does not have, so this is expected to fail rather than succeed.
            var ex = await Assert.ThrowsAsync<ApifyApiException>(() => tc.PublishAsync());
            Assert.True(ex.StatusCode is 400 or 403);
        }
        finally
        {
            await client.Task(task.Id!).DeleteAsync();
        }
    }
}
