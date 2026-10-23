using System;
using System.Threading.Tasks;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// A run's and a build's <c>WithTimeout</c> override must reach the requests of the nested storage and log
/// clients obtained from them afterwards, including through a last-run client.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RunNestedClientTimeoutTests
{
    private static readonly TimeSpan Override = TimeSpan.FromSeconds(3);

    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    private static string Dataset() => SpecFixtures.Dataset();

    [Fact]
    public async Task RunDatasetInheritsTheTimeout()
    {
        var transport = new MockTransport().QueueResponse(200, Dataset());
        await Client(transport).Run("r1").WithTimeout(Override).Dataset().GetAsync();
        Assert.Equal(3, transport.Timeouts[0]);
    }

    [Fact]
    public async Task RunKeyValueStoreInheritsTheTimeout()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"keyValue\":1}}").QueueResponse(200, "x");
        await Client(transport).Run("r1").WithTimeout(Override).KeyValueStore().RecordExistsAsync("k");
        Assert.All(transport.Timeouts, static t => Assert.Equal(3, t));
    }

    [Fact]
    public async Task RunRequestQueueInheritsTheTimeout()
    {
        var transport = new MockTransport().QueueResponse(200, SpecFixtures.QueueHead());
        await Client(transport).Run("r1").WithTimeout(Override).RequestQueue().ListHeadAsync(1);
        Assert.Equal(3, transport.Timeouts[0]);
    }

    [Fact]
    public async Task RunLogInheritsTheTimeout()
    {
        var transport = new MockTransport().QueueResponse(200, "log text");
        await Client(transport).Run("r1").WithTimeout(Override).Log().GetAsync();
        Assert.Equal(3, transport.Timeouts[0]);
    }

    [Fact]
    public async Task BuildLogInheritsTheTimeout()
    {
        var transport = new MockTransport().QueueResponse(200, "log text");
        await Client(transport).Build("b1").WithTimeout(Override).Log().GetAsync();
        Assert.Equal(3, transport.Timeouts[0]);
    }

    [Fact]
    public async Task LastRunDatasetInheritsTheTimeout()
    {
        var transport = new MockTransport().QueueResponse(200, Dataset());
        await Client(transport).Actor("me~act").LastRun().WithTimeout(Override).Dataset().GetAsync();
        Assert.Equal(3, transport.Timeouts[0]);
    }

    [Fact]
    public async Task IteratingALastRunPinnedToAnotherRunKeepsTheTimeout()
    {
        // The first request resolves `runs/last` to a concrete run; the pinned client used for the dataset
        // requests must carry the same override.
        var transport = new MockTransport()
            .QueueResponse(200, SpecFixtures.Run("resolved"))
            .QueueResponse(200, Dataset())
            .QueueResponse(200, "[]");

        await foreach (var unused in Client(transport).Actor("me~act").LastRun().WithTimeout(Override).IterateDatasetItemsAsync())
        {
            _ = unused;
        }

        Assert.Contains("/actor-runs/resolved/dataset", transport.Received[1].Uri, StringComparison.Ordinal);
        Assert.All(transport.Timeouts, static t => Assert.Equal(3, t));
    }
}
