using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// The dataset items endpoint only accepts a JSON object or an array of objects, so the client rejects any
/// other shape before sending a request, matching the reference client's narrowed <c>pushItems()</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PushItemsValidationTests
{
    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    public static IEnumerable<object[]> ValidItems()
    {
        yield return new object[] { new { a = 1 }, "{\"a\":1}" };
        yield return new object[] { new[] { new { a = 1 }, new { a = 2 } }, "[{\"a\":1},{\"a\":2}]" };
        yield return new object[] { new JsonObject { ["a"] = 1 }, "{\"a\":1}" };
        yield return new object[] { new List<object> { new { a = 1 } }, "[{\"a\":1}]" };
        yield return new object[] { Array.Empty<object>(), "[]" };
    }

    public static IEnumerable<object[]> InvalidItems()
    {
        yield return new object[] { 5 };
        yield return new object[] { true };
        yield return new object[] { new[] { 1, 2 } };
        yield return new object[] { new object[] { new { a = 1 }, "text" } };
        yield return new object[] { new object[] { new[] { new { a = 1 } } } };
    }

    [Theory]
    [MemberData(nameof(ValidItems))]
    public async Task AnObjectOrAnArrayOfObjectsIsSent(object items, string expectedBody)
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);

        await Client(transport).Dataset("ds1").PushItemsAsync(items);

        Assert.Equal(expectedBody, transport.LastRequest.Body);
    }

    [Theory]
    [MemberData(nameof(InvalidItems))]
    public async Task AnyOtherShapeIsRejectedBeforeARequestIsSent(object items)
    {
        var transport = new MockTransport();

        await Assert.ThrowsAsync<ArgumentException>(() => Client(transport).Dataset("ds1").PushItemsAsync(items));

        Assert.Equal(0, transport.CallCount);
    }

    [Fact]
    public async Task AStringIsSentAsAlreadySerializedJson()
    {
        var transport = new MockTransport().QueueResponse(200, string.Empty);

        await Client(transport).Dataset("ds1").PushItemsAsync("[{\"a\":1}]");

        Assert.Equal("[{\"a\":1}]", transport.LastRequest.Body);
    }
}
