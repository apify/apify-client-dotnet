using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Offline tests for the ambiguous-404 distinction (mirrors the reference client's
/// <c>catchNotFoundForResourceOrThrow()</c>): a resource addressed by its own id resolves a 404 to
/// <c>null</c>/no-op, while a resource reached through a chained client with no id of its own (e.g. a run's
/// default dataset) throws, since the 404 could mean either the parent or the nested resource is missing.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AmbiguousNotFoundTests
{
    private const string NotFoundBody = "{\"error\":{\"type\":\"record-not-found\",\"message\":\"not found\"}}";

    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task IdAddressedDatasetGetResolvesNullOn404()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        Assert.Null(await Client(transport).Dataset("missing").GetAsync());
    }

    [Fact]
    public async Task RunNestedDatasetGetThrowsOn404()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => Client(transport).Run("run1").Dataset().GetAsync());
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task RunNestedKeyValueStoreDeleteThrowsOn404()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        await Assert.ThrowsAsync<NotFoundException>(
            () => Client(transport).Run("run1").KeyValueStore().DeleteAsync());
    }

    [Fact]
    public async Task RunNestedRequestQueueGetRequestByIdStillResolvesNullOn404()
    {
        // A request looked up by its own id is never ambiguous, even through a run-nested queue client:
        // the 404 unambiguously means that request is missing, not the run or the queue.
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        Assert.Null(await Client(transport).Run("run1").RequestQueue().GetRequestAsync("req1"));
    }

    [Fact]
    public async Task IdAddressedLogGetResolvesNullOn404()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        Assert.Null(await Client(transport).Log("missing").GetAsync());
    }

    [Fact]
    public async Task RunNestedLogGetThrowsOn404()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        await Assert.ThrowsAsync<NotFoundException>(() => Client(transport).Run("run1").Log().GetAsync());
    }

    [Fact]
    public async Task BuildNestedLogGetThrowsOn404()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        await Assert.ThrowsAsync<NotFoundException>(() => Client(transport).Build("build1").Log().GetAsync());
    }

    [Fact]
    public async Task IdAddressedLogStreamResolvesNullOn404()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        Assert.Null(await Client(transport).Log("missing").StreamAsync());
    }

    [Fact]
    public async Task RunNestedLogStreamThrowsOn404()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        await Assert.ThrowsAsync<NotFoundException>(() => Client(transport).Run("run1").Log().StreamAsync());
    }

    [Fact]
    public async Task DatasetGetStatisticsThrowsOn404InsteadOfResolvingNull()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        await Assert.ThrowsAsync<NotFoundException>(() => Client(transport).Dataset("missing").GetStatisticsAsync());
    }

    [Fact]
    public async Task ScheduleGetLogThrowsOn404InsteadOfResolvingNull()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        await Assert.ThrowsAsync<NotFoundException>(() => Client(transport).Schedule("missing").GetLogAsync());
    }

    [Fact]
    public async Task TaskGetInputThrowsOn404InsteadOfResolvingNull()
    {
        var transport = new MockTransport().QueueResponse(404, NotFoundBody);
        await Assert.ThrowsAsync<NotFoundException>(() => Client(transport).Task("missing").GetInputAsync());
    }
}
