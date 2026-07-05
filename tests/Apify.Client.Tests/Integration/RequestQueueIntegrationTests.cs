using System.Collections.Generic;
using System.Threading.Tasks;
using Apify.Client.Models;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class RequestQueueIntegrationTests : IntegrationTestBase
{
    [SkippableFact]
    public async Task ListRequestQueues()
    {
        var client = RequireClient();
        var page = await client.RequestQueues().ListAsync(new StorageListOptions { Limit = 5 });
        Assert.True(page.Items.Count <= 5);
        Assert.Equal(page.Items.Count, (int)page.Count);
        Assert.True(page.Total >= page.Items.Count);
    }

    [SkippableFact]
    public async Task GetRequestQueue()
    {
        var client = RequireClient();
        var rq = await client.RequestQueues().GetOrCreateAsync(UniqueName("rq-get"));
        try
        {
            var got = await client.RequestQueue(rq.Id!).GetAsync();
            Assert.NotNull(got);
            Assert.Equal(rq.Id, got!.Id);
        }
        finally
        {
            await client.RequestQueue(rq.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task RequestQueueCrudFlow()
    {
        var client = RequireClient();
        var rq = await client.RequestQueues().GetOrCreateAsync(UniqueName("rq-crud"));
        try
        {
            var queue = client.RequestQueue(rq.Id!);
            Assert.NotNull(await queue.GetAsync());

            var request = new RequestQueueRequest("https://example.com", "example") { Method = "GET" };
            var info = await queue.AddRequestAsync(request);
            Assert.False(string.IsNullOrEmpty(info.RequestId));

            var got = await queue.GetRequestAsync(info.RequestId!);
            Assert.NotNull(got);
            Assert.Equal("https://example.com", got!.Url);

            Assert.NotEmpty((await queue.ListHeadAsync(10)).Items);
            await queue.UpdateAsync(new { name = UniqueName("rq-renamed") });
            await queue.DeleteRequestAsync(info.RequestId!);
        }
        finally
        {
            await client.RequestQueue(rq.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task RequestQueuePaginateMultiplePages()
    {
        var client = RequireClient();
        var rq = await client.RequestQueues().GetOrCreateAsync(UniqueName("rq-page"));
        try
        {
            var queue = client.RequestQueue(rq.Id!);
            const int total = 5;
            for (var i = 0; i < total; i++)
            {
                var url = "https://example.com/" + i;
                await queue.AddRequestAsync(new RequestQueueRequest(url, url));
            }

            var seen = new HashSet<string>();
            await foreach (var request in queue.PaginateRequestsAsync(new PaginateRequestsOptions { MaxPageLimit = 2 }))
            {
                seen.Add(request.Url!);
            }

            Assert.Equal(total, seen.Count);
        }
        finally
        {
            await client.RequestQueue(rq.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task RequestQueueBatchAddRequests()
    {
        var client = RequireClient();
        var rq = await client.RequestQueues().GetOrCreateAsync(UniqueName("rq-batch"));
        try
        {
            var queue = client.RequestQueue(rq.Id!);
            const int total = 30; // > 25, so the client must split into multiple chunks
            var requests = new List<RequestQueueRequest>();
            for (var i = 0; i < total; i++)
            {
                var url = "https://batch.example.com/" + i;
                requests.Add(new RequestQueueRequest(url, url));
            }

            var result = await queue.BatchAddRequestsAsync(requests);
            Assert.Equal(total, result.ProcessedRequests.Count);
            Assert.Empty(result.UnprocessedRequests);
        }
        finally
        {
            await client.RequestQueue(rq.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task RequestQueueLockLifecycle()
    {
        var client = RequireClient();
        var rq = await client.RequestQueues().GetOrCreateAsync(UniqueName("rq-lock"));
        try
        {
            var queue = client.RequestQueue(rq.Id!).WithClientKey("dotnet-test-client-key");
            var info = await queue.AddRequestAsync(new RequestQueueRequest("https://lock.example.com", "lock"));
            Assert.True((await queue.ListRequestsAsync(new ListRequestsOptions())).ContainsKey("items"));
            await queue.ListRequestsAsync(new ListRequestsOptions
            {
                Filter = new[] { ListRequestsOptions.FilterLocked, ListRequestsOptions.FilterPending },
            });
            Assert.True((await queue.ListAndLockHeadAsync(60, 10)).ContainsKey("items"));
            await queue.ProlongRequestLockAsync(info.RequestId!, 30);
            await queue.DeleteRequestLockAsync(info.RequestId!);
            await queue.UnlockRequestsAsync();
        }
        finally
        {
            await client.RequestQueue(rq.Id!).DeleteAsync();
        }
    }
}
