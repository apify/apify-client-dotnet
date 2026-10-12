using System;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Internal;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Offline tests for <c>ActorStartOptions.WaitForResources</c>/<c>TaskStartOptions.WaitForResources</c>:
/// retrying a run start while the account has no free memory or concurrent-run slot for it.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WaitForResourcesTests
{
    private const string MemoryLimitBody =
        "{\"error\":{\"type\":\"actor-memory-limit-exceeded\",\"message\":\"not enough memory\"}}";

    private const string ConcurrentRunsLimitBody =
        "{\"error\":{\"type\":\"concurrent-runs-limit-exceeded\",\"message\":\"too many concurrent runs\"}}";

    private const string InvalidInputBody =
        "{\"error\":{\"type\":\"invalid-input\",\"message\":\"bad input\"}}";

    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    // ---- ResourceLimitRetry (the shared retry primitive) -----------------------------------------------

    [Fact]
    public async Task DisabledByDefaultThrowsTheFirstResourceLimitErrorImmediately()
    {
        var attempts = 0;
        Task<int> Start()
        {
            attempts++;
            throw MakeError("actor-memory-limit-exceeded");
        }

        await Assert.ThrowsAsync<ApifyApiException>(
            () => ResourceLimitRetry.StartAsync(Start, waitForResources: null, CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task UntilAvailableRetriesPastResourceLimitErrorsThenReturnsTheResult()
    {
        var attempts = 0;
        Task<string> Start()
        {
            attempts++;
            if (attempts < 3)
            {
                throw MakeError(attempts == 1 ? "actor-memory-limit-exceeded" : "concurrent-runs-limit-exceeded");
            }

            return Task.FromResult("started");
        }

        // A tiny cooldown keeps this test fast; production call sites use the real 10s cooldown.
        var result = await ResourceLimitRetry.StartAsync(Start, Options.WaitForResources.UntilAvailable, CancellationToken.None, cooldownMillis: 1);

        Assert.Equal("started", result);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task BoundedWaitThrowsTheLastErrorOnceTheBudgetIsExhausted()
    {
        var attempts = 0;
        Task<int> Start()
        {
            attempts++;
            throw MakeError("actor-memory-limit-exceeded");
        }

        // A zero-second budget exhausts on the very first failure without any real wait.
        var error = await Assert.ThrowsAsync<ApifyApiException>(
            () => ResourceLimitRetry.StartAsync(Start, Options.WaitForResources.For(0), CancellationToken.None));

        Assert.Equal("actor-memory-limit-exceeded", error.Type);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task NonResourceLimitErrorsAreNeverRetried()
    {
        var attempts = 0;
        Task<int> Start()
        {
            attempts++;
            throw MakeError("invalid-input");
        }

        await Assert.ThrowsAsync<ApifyApiException>(
            () => ResourceLimitRetry.StartAsync(Start, Options.WaitForResources.UntilAvailable, CancellationToken.None, cooldownMillis: 1));

        Assert.Equal(1, attempts);
    }

    private static ApifyApiException MakeError(string type) =>
        new(402, type, "test error", attempt: 1, httpMethod: "POST", path: "/v2/acts/act/runs");

    // ---- WaitForResources value type ---------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ForRejectsNegativeNaNAndInfiniteBudgets(double invalidSecs)
    {
        // PositiveInfinity would silently behave like UntilAvailable; reject it so the two "retry
        // forever" spellings stay distinct and deliberate.
        Assert.Throws<ArgumentOutOfRangeException>(() => Options.WaitForResources.For(invalidSecs));
    }

    [Fact]
    public void ForAcceptsZeroAndFiniteBudgets()
    {
        Assert.Equal(0, Options.WaitForResources.For(0).MaxWaitSecs);
        Assert.Equal(30, Options.WaitForResources.For(30).MaxWaitSecs);
    }

    // ---- ActorClient / TaskClient wiring ----------------------------------------------------------------

    [Fact]
    public async Task ActorStartWithoutWaitForResourcesThrowsOnFirstResourceLimitError()
    {
        var transport = new MockTransport().QueueResponse(402, MemoryLimitBody);

        var error = await Assert.ThrowsAsync<ApifyApiException>(
            () => Client(transport).Actor("act").StartAsync());

        Assert.Equal("actor-memory-limit-exceeded", error.Type);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task ActorStartNeverSendsWaitForResourcesAsAQueryParameter()
    {
        // waitForResources is a client-side retry policy, not an API parameter; the retry loop's cooldown is
        // a real 10s per attempt in production (see ResourceLimitRetry), which the generic retry tests above
        // already cover with an injectable cooldown, so this only checks the request shape of a single
        // (immediately failing) attempt.
        var transport = new MockTransport().QueueResponse(402, MemoryLimitBody);

        var error = await Assert.ThrowsAsync<ApifyApiException>(
            () => Client(transport).Actor("act").StartAsync(null, new ActorStartOptions { WaitForResources = Options.WaitForResources.For(0) }));

        Assert.Equal("actor-memory-limit-exceeded", error.Type);
        Assert.DoesNotContain("waitForResources", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TaskStartWithoutWaitForResourcesThrowsOnFirstResourceLimitError()
    {
        var transport = new MockTransport().QueueResponse(402, ConcurrentRunsLimitBody);

        var error = await Assert.ThrowsAsync<ApifyApiException>(
            () => Client(transport).Task("task1").StartAsync());

        Assert.Equal("concurrent-runs-limit-exceeded", error.Type);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task TaskStartNeverSendsWaitForResourcesAsAQueryParameter()
    {
        var transport = new MockTransport().QueueResponse(402, MemoryLimitBody);

        var error = await Assert.ThrowsAsync<ApifyApiException>(
            () => Client(transport).Task("task1").StartAsync(null, new TaskStartOptions { WaitForResources = Options.WaitForResources.For(0) }));

        Assert.Equal("actor-memory-limit-exceeded", error.Type);
        Assert.DoesNotContain("waitForResources", transport.LastRequest.Uri, StringComparison.Ordinal);
    }
}
