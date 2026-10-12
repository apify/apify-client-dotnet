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
        // Regression test (mirrors a reference-client bug fix): a run id that never existed used to make
        // WaitForFinishAsync treat every 404 as transient database replica lag and poll for the entire wait
        // budget (up to ~11.5 days when waitSecs is left unset). It must instead give up a few seconds into
        // a *persistent* 404, well before a much larger waitSecs elapses.
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
        // Regression test: when `waitSecs` is small enough that the overall wait budget runs out before the
        // 3-second not-found grace window does, the loop exits via the budget check rather than the grace-
        // window check. It must still surface the persistent 404 as NotFoundException, not the generic
        // "cannot fetch details from the server" fallback that only applies when nothing was ever fetched
        // and no error was ever recorded (which cannot happen once a 404 has been seen).
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
            .QueueResponse(200, "{\"data\":{\"id\":\"run1\",\"status\":\"SUCCEEDED\"}}");

        var run = await Client(transport).Run("run1").WaitForFinishAsync(waitSecs: 30);

        Assert.Equal("SUCCEEDED", run.Status);
        Assert.Equal(2, transport.CallCount);
    }
}
