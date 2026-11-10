using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Offline tests for <c>RunClient.WaitForFinishAsync</c>'s handling of a run/build that keeps answering 404.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WaitForFinishTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 30,
        HttpTransport = transport,
    });

    [Fact]
    public async Task WaitForFinishThrowsNotFoundOnPersistent404InsteadOfRunningOutTheFullWaitBudget()
    {
        // Regression test, as a reference client bug fix: a run ID that never existed made WaitForFinishAsync treat every 404 as
        // replica lag and poll for the whole wait (about 11.5 days without waitSecs). It must give up after a persistent 404.
        var transport = new MockTransport();
        for (var i = 0; i < 60; i++)
        {
            transport.QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"Run was not found\"}}");
        }

        var stopwatch = Stopwatch.StartNew();
        await Assert.ThrowsAsync<NotFoundException>(
            () => Client(transport).Run("does-not-exist").WaitForFinishAsync(waitSecs: 100));
        stopwatch.Stop();

        // Gives up a few seconds into the persistent 404, nowhere near the 100s budget passed above.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task WaitForFinishThrowsNotFoundWhenTheWaitBudgetElapsesBeforeTheNotFoundGraceWindow()
    {
        // Regression test: when waitSecs runs out before the 3-second not-found window, the persistent 404 must still surface as
        // NotFoundException, not the generic fallback for when nothing was ever fetched.
        var transport = new MockTransport();
        for (var i = 0; i < 10; i++)
        {
            transport.QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"Run was not found\"}}");
        }

        var error = await Assert.ThrowsAsync<NotFoundException>(
            () => Client(transport).Run("does-not-exist").WaitForFinishAsync(waitSecs: 1));

        Assert.Equal("record-not-found", error.Type);
    }

    [Fact]
    public async Task WaitForFinishToleratesABriefNotFoundThenSucceeds()
    {
        // A short run of 404s (shorter than the not-found grace window) is replica lag, not a missing run:
        // once the resource starts resolving, the run completes normally.
        var transport = new MockTransport()
            .QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"Run was not found\"}}")
            .QueueResponse(200, SpecFixtures.Run("run1"));

        var run = await Client(transport).Run("run1").WaitForFinishAsync(waitSecs: 30);

        Assert.Equal("SUCCEEDED", run.Status);
        Assert.Equal(2, transport.CallCount);
    }
}
