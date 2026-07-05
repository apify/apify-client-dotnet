using System;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class HttpClientTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "test-token",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task AuthAndUserAgentHeadersAreSent()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"abc\"}}");
        await Client(transport).Actor("abc").GetAsync();

        Assert.Equal("Bearer test-token", transport.LastRequest.Header("Authorization"));
        Assert.StartsWith("ApifyClient/", transport.LastRequest.Header("User-Agent"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task DataEnvelopeIsUnwrapped()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"act1\",\"name\":\"my-actor\"}}");
        var actor = await Client(transport).Actor("act1").GetAsync();

        Assert.NotNull(actor);
        Assert.Equal("act1", actor!.Id);
        Assert.Equal("my-actor", actor.Name);
    }

    [Fact]
    public async Task NotFoundReturnsNull()
    {
        var transport = new MockTransport().QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"not here\"}}");
        Assert.Null(await Client(transport).Actor("missing").GetAsync());
    }

    [Fact]
    public async Task ServerErrorsAreRetriedThenSucceed()
    {
        var transport = new MockTransport()
            .QueueResponse(500, "{\"error\":{\"type\":\"server\",\"message\":\"boom\"}}")
            .QueueResponse(200, "{\"data\":{\"id\":\"ok\"}}");

        var actor = await Client(transport).Actor("x").GetAsync();
        Assert.Equal("ok", actor!.Id);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task ValidationErrorIsNotRetriedAndThrows()
    {
        var transport = new MockTransport().QueueResponse(400, "{\"error\":{\"type\":\"bad-input\",\"message\":\"invalid\"}}");

        var ex = await Assert.ThrowsAsync<ApifyApiException>(() => Client(transport).Actors().CreateAsync(new { name = "x" }));
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("bad-input", ex.Type);
        Assert.Contains("invalid", ex.ApiMessage, StringComparison.Ordinal);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task TransportErrorsAreRetried()
    {
        var transport = new MockTransport()
            .QueueError()
            .QueueResponse(200, "{\"data\":{\"id\":\"recovered\"}}");
        var actor = await Client(transport).Actor("x").GetAsync();
        Assert.Equal("recovered", actor!.Id);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task BooleanQueryParamsEncodedAsOneZero()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[],\"total\":0}}");
        await Client(transport).Actors().ListAsync(new ActorListOptions { My = true, Limit = 5 });

        var uri = transport.LastRequest.Uri;
        Assert.Contains("my=1", uri, StringComparison.Ordinal);
        Assert.Contains("limit=5", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListUnwrapsPaginationEnvelope()
    {
        const string body = "{\"data\":{\"total\":2,\"offset\":0,\"limit\":10,\"count\":2,\"desc\":false,\"items\":[{\"id\":\"a\"},{\"id\":\"b\"}]}}";
        var transport = new MockTransport().QueueResponse(200, body);
        var page = await Client(transport).Actors().ListAsync();

        Assert.Equal(2, page.Total);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("a", page.Items[0].Id);
    }

    [Fact]
    public async Task PaginationCountReflectsItemsNotServerMetadata()
    {
        // Server reports total/count larger than the items actually returned in this page. Count and the
        // indexer must operate on the item array so `for (i < Count) page[i]` cannot throw; Total keeps the
        // API's reported total.
        const string body = "{\"data\":{\"total\":100,\"offset\":0,\"limit\":2,\"count\":100,\"desc\":false,\"items\":[{\"id\":\"a\"},{\"id\":\"b\"}]}}";
        var transport = new MockTransport().QueueResponse(200, body);
        var page = await Client(transport).Actors().ListAsync();

        Assert.Equal(100, page.Total);
        Assert.Equal(2, page.Count);
        Assert.Equal(2, page.Items.Count);
        for (var i = 0; i < page.Count; i++)
        {
            Assert.NotNull(page[i]); // must not throw IndexOutOfRange
        }
    }

    [Fact]
    public async Task DatasetItemsUseHeaderPagination()
    {
        var headers = new System.Collections.Generic.Dictionary<string, string>
        {
            ["X-Apify-Pagination-Total"] = "42",
            ["X-Apify-Pagination-Offset"] = "0",
            ["X-Apify-Pagination-Limit"] = "3",
        };
        var transport = new MockTransport().QueueResponse(200, "[{\"n\":1},{\"n\":2},{\"n\":3}]", headers);
        var page = await Client(transport).Dataset("ds1").ListItemsAsync();

        Assert.Equal(42, page.Total);
        Assert.Equal(3, page.Count);
        Assert.Equal(1, page.Items[0]!["n"]!.GetValue<int>());
    }

    [Fact]
    public async Task ValidateInputParsesBareObject()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"valid\":true}");
        Assert.True(await Client(transport).Actor("apify/hello-world").ValidateInputAsync(new { x = 1 }));
    }

    [Fact]
    public async Task SafeIdReplacesFirstSlashWithTilde()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"x\"}}");
        await Client(transport).Actor("apify/hello-world").GetAsync();
        Assert.Contains("/actors/apify~hello-world", transport.LastRequest.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RateLimitIsRetriedThenSucceeds()
    {
        // 429 (rate limit) must be retried just like a 5xx, then succeed on the next attempt.
        var transport = new MockTransport()
            .QueueResponse(429, "{\"error\":{\"type\":\"rate-limit-exceeded\",\"message\":\"slow down\"}}")
            .QueueResponse(200, "{\"data\":{\"id\":\"ok\"}}");

        var actor = await Client(transport).Actor("x").GetAsync();
        Assert.Equal("ok", actor!.Id);
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task ServerErrorsThrowAfterRetriesAreExhausted()
    {
        // maxRetries=2 => 3 attempts; every attempt is a 5xx, so the last error is thrown.
        var options = new ApifyClientOptions
        {
            Token = "t",
            MinDelayBetweenRetriesMillis = 1,
            TimeoutSecs = 5,
            MaxRetries = 2,
            HttpTransport = new MockTransport()
                .QueueResponse(500, "{\"error\":{\"type\":\"server\",\"message\":\"boom\"}}")
                .QueueResponse(500, "{\"error\":{\"type\":\"server\",\"message\":\"boom\"}}")
                .QueueResponse(500, "{\"error\":{\"type\":\"server\",\"message\":\"boom\"}}"),
        };
        var transport = (MockTransport)options.HttpTransport;

        var ex = await Assert.ThrowsAsync<ApifyApiException>(() => new ApifyClient(options).Actor("x").GetAsync());
        Assert.Equal(500, ex.StatusCode);
        Assert.Equal(3, ex.Attempt);
        Assert.Equal(3, transport.CallCount);
    }

    [Fact]
    public async Task TransportErrorsThrowAfterRetriesAreExhausted()
    {
        // maxRetries=2 => 3 attempts; every attempt is a transport failure, so it is finally rethrown.
        var options = new ApifyClientOptions
        {
            Token = "t",
            MinDelayBetweenRetriesMillis = 1,
            TimeoutSecs = 5,
            MaxRetries = 2,
            HttpTransport = new MockTransport().QueueError().QueueError().QueueError(),
        };
        var transport = (MockTransport)options.HttpTransport;

        await Assert.ThrowsAsync<ApifyTransportException>(() => new ApifyClient(options).Actor("x").GetAsync());
        Assert.Equal(3, transport.CallCount);
    }

    [Fact]
    public async Task AttemptTimeoutDoublesPerRetryAndCapsAtOverallBudget()
    {
        // Per-call base timeout (5s) is below the overall budget (100s), so each retry doubles the
        // per-attempt timeout until it would exceed the overall budget, at which point it is capped.
        var transport = new MockTransport();
        for (var i = 0; i < 6; i++)
        {
            transport.QueueResponse(500, "{\"error\":{\"type\":\"server\",\"message\":\"boom\"}}");
        }

        var client = new ApifyClient(new ApifyClientOptions
        {
            Token = "t",
            MinDelayBetweenRetriesMillis = 1,
            TimeoutSecs = 100, // overall budget
            MaxRetries = 5, // 6 attempts total
            HttpTransport = transport,
        });

        // A per-queue timeout of 5s becomes the base per-attempt timeout that then doubles.
        await Assert.ThrowsAsync<ApifyApiException>(
            () => client.RequestQueue("q1", new Options.RequestQueueClientOptions { TimeoutSecs = 5 }).ListHeadAsync(5));

        Assert.Equal(new[] { 5.0, 10.0, 20.0, 40.0, 80.0, 100.0 }, transport.Timeouts);
    }

    [Fact]
    public async Task TimeoutIsNotRetriedWhenDoNotRetryTimeoutsIsSet()
    {
        // A single timeout, then a success that must never be reached because retrying is opted out.
        var transport = new MockTransport().QueueError(timeout: true).QueueResponse(200, string.Empty);

        await Assert.ThrowsAsync<ApifyTransportException>(() => Client(transport)
            .KeyValueStore("s1")
            .SetRecordAsync("k", new byte[] { 1, 2, 3 }, "application/octet-stream", new SetRecordOptions { DoNotRetryTimeouts = true }));
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task TimeoutIsRetriedWhenDoNotRetryTimeoutsIsNotSet()
    {
        // With the default (DoNotRetryTimeouts=false) a timeout is retryable, so the retry succeeds.
        var transport = new MockTransport().QueueError(timeout: true).QueueResponse(200, string.Empty);

        await Client(transport)
            .KeyValueStore("s1")
            .SetRecordAsync("k", new byte[] { 1, 2, 3 }, "application/octet-stream", new SetRecordOptions());
        Assert.Equal(2, transport.CallCount);
    }

    [Fact]
    public async Task NullQueryParamsAreOmitted()
    {
        // Only Limit is set; every other (null) option must be absent from the query string entirely.
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"items\":[],\"total\":0}}");
        await Client(transport).Actors().ListAsync(new ActorListOptions { Limit = 5 });

        var uri = transport.LastRequest.Uri;
        Assert.Contains("limit=5", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("offset=", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("desc=", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("my=", uri, StringComparison.Ordinal);
        Assert.DoesNotContain("sortBy=", uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecordExistsReturnsFalseOnHeadNotFound()
    {
        // IsNotFound treats any 404 to a HEAD request as "not found" even without an error type.
        var transport = new MockTransport().QueueResponse(404, string.Empty);
        Assert.False(await Client(transport).KeyValueStore("s1").RecordExistsAsync("missing"));
        Assert.Equal("HEAD", transport.LastRequest.Method);
    }

    [Fact]
    public async Task GetRecordReturnsNullOnRecordOrTokenNotFound()
    {
        // The "record-or-token-not-found" error type is one of the IsNotFound branches -> null, not throw.
        var transport = new MockTransport().QueueResponse(404, "{\"error\":{\"type\":\"record-or-token-not-found\",\"message\":\"nope\"}}");
        Assert.Null(await Client(transport).KeyValueStore("s1").GetRecordAsync("missing"));
    }
}
