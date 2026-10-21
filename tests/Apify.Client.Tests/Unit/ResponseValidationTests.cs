using System;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Verifies how a successful (2xx) response that is not what the API specification promises is reported:
/// unparsable JSON as <see cref="InvalidResponseBodyException"/>, a missing <c>data</c> envelope or a
/// wrongly-typed <c>data</c> as <see cref="ResponseValidationException"/>, both with the request context.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResponseValidationTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task UnparsableBodyExceptionCarriesStatusMethodPathAndBody()
    {
        var transport = new MockTransport().QueueResponse(200, "<html>oops</html>");

        var ex = await Assert.ThrowsAsync<InvalidResponseBodyException>(() => Client(transport).Actor("me~act").GetAsync());

        Assert.Equal(200, ex.StatusCode);
        Assert.Equal("GET", ex.HttpMethod);
        Assert.EndsWith("/actors/me~act", ex.Path, StringComparison.Ordinal);
        Assert.Equal("<html>oops</html>", ex.Body);
    }

    [Fact]
    public async Task MissingDataEnvelopeOnGetThrowsInsteadOfLookingLikeNotFound()
    {
        var transport = new MockTransport().QueueResponse(200, "{}");

        var ex = await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Actor("me~act").GetAsync());

        Assert.IsAssignableFrom<ApifyClientException>(ex);
        Assert.Equal(200, ex.StatusCode);
        Assert.Equal("GET", ex.HttpMethod);
        Assert.Equal("{}", ex.Body);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task NullDataOnGetThrows()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":null}");

        await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Actor("me~act").GetAsync());
    }

    [Fact]
    public async Task ArrayDataWhereAnObjectIsExpectedThrowsOnUpdate()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":[]}");

        var ex = await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Actor("me~act").UpdateAsync(new { title = "x" }));

        Assert.Equal("PUT", ex.HttpMethod);
    }

    [Fact]
    public async Task MissingDataEnvelopeOnCreateThrows()
    {
        var transport = new MockTransport().QueueResponse(201, "{\"id\":\"a\"}");

        var ex = await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Actors().CreateAsync(new { name = "x" }));

        Assert.Equal("POST", ex.HttpMethod);
        Assert.Equal(201, ex.StatusCode);
    }

    [Fact]
    public async Task NonArrayDatasetItemsBodyThrows()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":[]}");

        await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Dataset("ds1").ListItemsAsync());
    }
}
