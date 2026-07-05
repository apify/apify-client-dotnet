using System;
using System.IO;
using System.Threading.Tasks;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class LogClientTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task GetLogByIdReturnsText()
    {
        var transport = new MockTransport().QueueResponse(200, "line1\nline2\n");
        var log = await Client(transport).Log("run1").GetAsync();

        Assert.Equal("line1\nline2\n", log);
        Assert.Equal("GET", transport.LastRequest.Method);
        Assert.Contains("/logs/run1", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingLogReturnsNull()
    {
        var transport = new MockTransport().QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"no log\"}}");
        Assert.Null(await Client(transport).Log("missing").GetAsync());
    }

    [Fact]
    public async Task RunNestedLogGet()
    {
        var transport = new MockTransport().QueueResponse(200, "run log");
        var log = await Client(transport).Run("run1").Log().GetAsync(new LogOptions { Raw = true });

        Assert.Equal("run log", log);
        var uri = transport.LastRequest.Uri;
        Assert.Contains("/actor-runs/run1/log", uri, StringComparison.Ordinal);
        Assert.Contains("raw=1", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StreamedLogUsesStreamQueryAndReturnsReadableStream()
    {
        var transport = new MockTransport().QueueResponse(200, "streamed log body");
        using var stream = await Client(transport).Run("run1").GetStreamedLogAsync();

        var uri = transport.LastRequest.Uri;
        Assert.Contains("/actor-runs/run1/log", uri, StringComparison.Ordinal);
        Assert.Contains("stream=1", uri, StringComparison.Ordinal);
        Assert.Contains("raw=1", uri, StringComparison.Ordinal);

        using var reader = new StreamReader(stream);
        Assert.Equal("streamed log body", await reader.ReadToEndAsync());
    }
}
