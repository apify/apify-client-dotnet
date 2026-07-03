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
}
