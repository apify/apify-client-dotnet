using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Apify.Client.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class StreamedLogTests
{
    private const string LogBody =
        "2024-01-02T03:04:05.678Z first message\n" +
        "2024-01-02T03:04:06.789Z second message\n" +
        "2024-01-02T03:04:07.890Z third message\n";

    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    private static async Task<List<string>> CollectAsync(ApifyClient client, bool fromStart)
    {
        var messages = new List<string>();
        var streamedLog = client.Run("run1").GetStreamedLog(
            m =>
            {
                lock (messages)
                {
                    messages.Add(m);
                }
            },
            fromStart);

        streamedLog.Start();
        // The mock returns a finite stream, so the background task drains and completes on its own; poll
        // until it has, then stop (which awaits the already-finished task).
        for (var i = 0; i < 200; i++)
        {
            await Task.Delay(5);
            lock (messages)
            {
                if (messages.Count >= 3)
                {
                    break;
                }
            }
        }

        await streamedLog.StopAsync();
        return messages;
    }

    [Fact]
    public async Task RedirectsEachCompleteMessageToSink()
    {
        var transport = new MockTransport().QueueResponse(200, LogBody);
        var messages = await CollectAsync(Client(transport), fromStart: true);

        Assert.Equal(3, messages.Count);
        Assert.StartsWith("2024-01-02T03:04:05.678Z", messages[0], StringComparison.Ordinal);
        Assert.Contains("first message", messages[0], StringComparison.Ordinal);
        Assert.Contains("third message", messages[2], StringComparison.Ordinal);

        var uri = transport.LastRequest.Uri;
        Assert.Contains("/actor-runs/run1/log", uri, StringComparison.Ordinal);
        Assert.Contains("stream=1", uri, StringComparison.Ordinal);
        Assert.Contains("raw=1", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FromStartFalseSkipsMessagesOlderThanCreation()
    {
        // All log lines are timestamped in the past, so with fromStart=false they are all filtered out.
        var transport = new MockTransport().QueueResponse(200, LogBody);
        var messages = new List<string>();
        var streamedLog = Client(transport).Run("run1").GetStreamedLog(
            m =>
            {
                lock (messages)
                {
                    messages.Add(m);
                }
            },
            fromStart: false);

        streamedLog.Start();
        await streamedLog.StopAsync();

        lock (messages)
        {
            Assert.Empty(messages);
        }
    }

    [Fact]
    public async Task StartTwiceThrows()
    {
        var transport = new MockTransport().QueueResponse(200, LogBody);
        var streamedLog = Client(transport).Run("run1").GetStreamedLog(_ => { });
        streamedLog.Start();
        Assert.Throws<InvalidOperationException>(() => streamedLog.Start());
        await streamedLog.StopAsync();
    }
}
