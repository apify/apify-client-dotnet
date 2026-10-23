using System;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Verifies the common <see cref="ApifyClientException"/> base lets every error this client raises for a
/// failed API call be caught with a single <c>catch (ApifyClientException)</c>, matching the reference
/// client's <c>ApifyClientError</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ApifyClientExceptionTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    [Fact]
    public async Task ApifyApiExceptionIsAnApifyClientException()
    {
        var transport = new MockTransport().QueueResponse(400, "{\"error\":{\"type\":\"bad-input\",\"message\":\"invalid\"}}");

        var ex = await Assert.ThrowsAsync<InvalidRequestException>(() => Client(transport).Actors().CreateAsync(new { name = "x" }));
        Assert.IsAssignableFrom<ApifyClientException>(ex);
        Assert.IsAssignableFrom<ApifyApiException>(ex);
    }

    [Fact]
    public async Task ApifyTransportExceptionIsAnApifyClientException()
    {
        var options = new ApifyClientOptions
        {
            Token = "t",
            MinDelayBetweenRetriesMillis = 1,
            TimeoutSecs = 5,
            MaxRetries = 0,
            HttpTransport = new MockTransport().QueueError(),
        };

        var ex = await Assert.ThrowsAsync<ApifyTransportException>(() => new ApifyClient(options).Actor("x").GetAsync());
        Assert.IsAssignableFrom<ApifyClientException>(ex);
    }

    [Fact]
    public async Task SuccessWithUnparsableBodyThrowsInvalidResponseBodyWithoutRetry()
    {
        var transport = new MockTransport().QueueResponse(200, "not json").QueueResponse(200, "{\"data\":{\"id\":\"a\"}}");

        var ex = await Assert.ThrowsAsync<InvalidResponseBodyException>(() => Client(transport).Actor("x").GetAsync());
        Assert.IsAssignableFrom<ApifyClientException>(ex);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task ErrorStatusWithUnparsableBodyThrowsApiExceptionForItsStatus()
    {
        var transport = new MockTransport().QueueResponse(400, "<html>Bad Request</html>");

        var ex = await Assert.ThrowsAsync<InvalidRequestException>(() => Client(transport).Actors().CreateAsync(new { name = "x" }));
        Assert.Contains("<html>Bad Request</html>", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatchingApifyClientExceptionCatchesBothFamilies()
    {
        // A single `catch (ApifyClientException)` must catch an API error...
        var apiTransport = new MockTransport().QueueResponse(404, "{\"error\":{\"type\":\"record-not-found\",\"message\":\"gone\"}}");
        await AssertThrowsApifyClientExceptionAsync(() => Client(apiTransport).Actor("missing").UpdateAsync(new { title = "x" }));

        // ...and a transport failure, without naming either concrete subclass.
        var transportOptions = new ApifyClientOptions
        {
            Token = "t",
            MinDelayBetweenRetriesMillis = 1,
            TimeoutSecs = 5,
            MaxRetries = 0,
            HttpTransport = new MockTransport().QueueError(),
        };
        await AssertThrowsApifyClientExceptionAsync(() => new ApifyClient(transportOptions).Actor("x").GetAsync());
    }

    /// <summary>
    /// Asserts <paramref name="action"/> throws something assignable to <see cref="ApifyClientException"/>,
    /// the way application code catching the common base would — without xUnit's exact-type
    /// <c>ThrowsAsync&lt;T&gt;</c>, which would reject the concrete subclasses this test exercises.
    /// </summary>
    private static async Task AssertThrowsApifyClientExceptionAsync(Func<Task> action)
    {
        var ex = await Record.ExceptionAsync(action);
        Assert.IsAssignableFrom<ApifyClientException>(ex);
    }
}
