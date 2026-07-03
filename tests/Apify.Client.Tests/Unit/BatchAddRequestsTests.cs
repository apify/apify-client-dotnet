using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Models;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Offline behavioral tests for <see cref="Apify.Client.Resources.RequestQueueClient.BatchAddRequestsAsync"/>:
/// uniqueKey validation, count/byte chunking, unprocessed-retry from a successful response, and the
/// non-throwing error contract, matching the JS reference.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BatchAddRequestsTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    /// <summary>
    /// No-delay, sequential options so retry/chunking tests that queue ordered responses stay
    /// deterministic (parallel dispatch is covered separately).
    /// </summary>
    private static BatchAddRequestsOptions FastOptions(int maxRetries = 3) =>
        new(maxUnprocessedRequestsRetries: maxRetries, maxParallel: 1, minDelayBetweenUnprocessedRequestsRetriesMillis: 0);

    private static string BatchResponse(IEnumerable<string> uniqueKeys, IEnumerable<string>? unprocessedKeys = null)
    {
        var processed = new JsonArray();
        foreach (var k in uniqueKeys)
        {
            processed.Add(new JsonObject
            {
                ["uniqueKey"] = k,
                ["requestId"] = "id-" + k,
                ["wasAlreadyPresent"] = false,
                ["wasAlreadyHandled"] = false,
            });
        }

        var unprocessed = new JsonArray();
        foreach (var k in unprocessedKeys ?? Array.Empty<string>())
        {
            unprocessed.Add(new JsonObject { ["uniqueKey"] = k, ["url"] = "https://x/" + k, ["method"] = "GET" });
        }

        return new JsonObject
        {
            ["data"] = new JsonObject { ["processedRequests"] = processed, ["unprocessedRequests"] = unprocessed },
        }.ToJsonString();
    }

    [Fact]
    public async Task MissingUniqueKeyThrowsBeforeAnyCall()
    {
        var transport = new MockTransport();
        var requests = new List<RequestQueueRequest> { new("https://a.com") };

        await Assert.ThrowsAsync<ArgumentException>(() => Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests));
        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task ApiErrorReportedAsUnprocessedNotThrown()
    {
        var transport = new MockTransport().QueueResponse(403, "{\"error\":{\"type\":\"insufficient-permissions\",\"message\":\"nope\"}}");
        var requests = new List<RequestQueueRequest> { new("https://a.com", "a") };

        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, FastOptions());

        Assert.Empty(result.ProcessedRequests);
        Assert.Single(result.UnprocessedRequests);
        Assert.Equal("a", result.UnprocessedRequests[0].UniqueKey);
    }

    [Fact]
    public async Task MultiChunkPreservesEarlierChunksWhenLaterChunkFails()
    {
        var keys = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            keys.Add("u" + i);
        }

        var transport = new MockTransport()
            .QueueResponse(200, BatchResponse(keys.GetRange(0, 25)))
            .QueueResponse(403, "{\"error\":{\"type\":\"x\",\"message\":\"boom\"}}");
        var requests = keys.ConvertAll(k => new RequestQueueRequest("https://x/" + k, k));

        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, FastOptions());

        Assert.Equal(25, result.ProcessedRequests.Count);
        Assert.Equal(5, result.UnprocessedRequests.Count);
    }

    [Fact]
    public async Task RetriesOnlyUnprocessedFromSuccessfulResponse()
    {
        var transport = new MockTransport()
            .QueueResponse(200, BatchResponse(new[] { "r0" }, new[] { "r1" }))
            .QueueResponse(200, BatchResponse(new[] { "r1" }));
        var requests = new List<RequestQueueRequest> { new("https://a.com", "r0"), new("https://b.com", "r1") };

        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, FastOptions());

        Assert.Equal(2, transport.CallCount);
        Assert.Equal(2, result.ProcessedRequests.Count);
        Assert.Empty(result.UnprocessedRequests);

        // The retry must send only the still-unprocessed request (r1), not the whole batch again.
        var retryBody = JsonNode.Parse(transport.Received[1].Body)!.AsArray();
        Assert.Single(retryBody);
        Assert.Equal("r1", retryBody[0]!["uniqueKey"]!.GetValue<string>());
    }

    [Fact]
    public async Task UnprocessedReportedAfterRetriesExhausted()
    {
        var transport = new MockTransport();
        for (var i = 0; i < 3; i++)
        {
            transport.QueueResponse(200, BatchResponse(Array.Empty<string>(), new[] { "r0" }));
        }

        var requests = new List<RequestQueueRequest> { new("https://a.com", "r0") };

        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, FastOptions(2));

        Assert.Equal(3, transport.CallCount); // 1 + 2 retries
        Assert.Empty(result.ProcessedRequests);
        Assert.Single(result.UnprocessedRequests);
        Assert.Equal("r0", result.UnprocessedRequests[0].UniqueKey);
    }

    [Fact]
    public async Task ChunksByCountLimit()
    {
        var keys = new List<string>();
        for (var i = 0; i < 30; i++)
        {
            keys.Add("u" + i);
        }

        var transport = new MockTransport()
            .QueueResponse(200, BatchResponse(keys.GetRange(0, 25)))
            .QueueResponse(200, BatchResponse(keys.GetRange(25, 5)));
        var requests = keys.ConvertAll(k => new RequestQueueRequest("https://x/" + k, k));

        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, FastOptions());

        Assert.Equal(2, transport.CallCount);
        Assert.Equal(30, result.ProcessedRequests.Count);
        Assert.Equal(25, JsonNode.Parse(transport.Received[0].Body)!.AsArray().Count);
    }

    [Fact]
    public async Task ChunksByPayloadByteSize()
    {
        var big = new string('x', 4 * 1024 * 1024);
        var keys = new[] { "b0", "b1", "b2" };
        var transport = new MockTransport()
            .QueueResponse(200, BatchResponse(new[] { "b0", "b1" }))
            .QueueResponse(200, BatchResponse(new[] { "b2" }));
        var requests = new List<RequestQueueRequest>();
        foreach (var k in keys)
        {
            requests.Add(new RequestQueueRequest("https://x/" + k, k) { UserData = new JsonObject { ["blob"] = big } });
        }

        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, FastOptions());

        Assert.Equal(2, transport.CallCount);
        Assert.Equal(3, result.ProcessedRequests.Count);
        Assert.Equal(2, JsonNode.Parse(transport.Received[0].Body)!.AsArray().Count); // byte limit, not the count limit
    }

    [Fact]
    public async Task DispatchesChunksWithBoundedParallelism()
    {
        var transport = new MockTransport { EchoBatchProcessed = true, ArtificialDelayMillis = 40 };
        var requests = new List<RequestQueueRequest>();
        for (var i = 0; i < 100; i++) // 100 requests -> 4 chunks of 25
        {
            requests.Add(new RequestQueueRequest("https://x/" + i, "k" + i));
        }

        var options = new BatchAddRequestsOptions(maxParallel: 4, minDelayBetweenUnprocessedRequestsRetriesMillis: 0);
        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, options);

        Assert.Equal(100, result.ProcessedRequests.Count);
        Assert.Empty(result.UnprocessedRequests);
        Assert.Equal(4, transport.CallCount);
        // With 4 chunks and maxParallel=4 the calls must overlap; strictly sequential dispatch would be 1.
        Assert.True(transport.MaxObservedConcurrency > 1, "expected concurrent batch calls");
    }

    [Fact]
    public async Task MaxParallelOneKeepsDispatchSequential()
    {
        var transport = new MockTransport { EchoBatchProcessed = true, ArtificialDelayMillis = 20 };
        var requests = new List<RequestQueueRequest>();
        for (var i = 0; i < 75; i++) // 75 requests -> 3 chunks of 25
        {
            requests.Add(new RequestQueueRequest("https://x/" + i, "k" + i));
        }

        var options = new BatchAddRequestsOptions(maxParallel: 1, minDelayBetweenUnprocessedRequestsRetriesMillis: 0);
        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, options);

        Assert.Equal(75, result.ProcessedRequests.Count);
        Assert.Equal(3, transport.CallCount);
        Assert.Equal(1, transport.MaxObservedConcurrency);
    }

    [Fact]
    public async Task ParallelResultsMergedInInputOrder()
    {
        var transport = new MockTransport { EchoBatchProcessed = true, ArtificialDelayMillis = 30 };
        var requests = new List<RequestQueueRequest>();
        for (var i = 0; i < 60; i++) // 60 requests -> 3 chunks (0..24, 25..49, 50..59)
        {
            requests.Add(new RequestQueueRequest("https://x/" + i, "k" + i));
        }

        var options = new BatchAddRequestsOptions(maxParallel: 3, minDelayBetweenUnprocessedRequestsRetriesMillis: 0);
        var result = await Client(transport).RequestQueue("q1").BatchAddRequestsAsync(requests, false, options);

        Assert.Equal(60, result.ProcessedRequests.Count);
        // Merge order must follow input order regardless of which chunk finished first.
        for (var i = 0; i < 60; i++)
        {
            Assert.Equal("k" + i, result.ProcessedRequests[i].UniqueKey);
        }
    }

    [Fact]
    public async Task OversizedSingleRequestThrows()
    {
        var huge = new string('x', 10 * 1024 * 1024); // > 9 MiB on its own
        var requests = new List<RequestQueueRequest> { new("https://a.com", "big") { UserData = new JsonObject { ["blob"] = huge } } };

        await Assert.ThrowsAsync<ArgumentException>(() => Client(new MockTransport()).RequestQueue("q1").BatchAddRequestsAsync(requests));
    }
}
