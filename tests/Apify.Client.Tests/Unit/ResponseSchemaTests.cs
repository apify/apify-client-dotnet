using System;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Internal;
using Xunit;

namespace Apify.Client.Tests.Unit;

/// <summary>
/// Verifies the validation of successful responses against the schemas of the Apify OpenAPI
/// specification: required properties, JSON types, nullability, unknown properties and values, and how the
/// failures surface through the resource clients.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ResponseSchemaTests
{
    private const string Build = "\"id\":\"b\",\"actId\":\"a\",\"userId\":\"u\",\"startedAt\":\"2026-01-01T00:00:00.000Z\",\"status\":\"SUCCEEDED\",\"meta\":{\"origin\":\"API\"},\"buildNumber\":\"0.0.1\"";

    private static ApifyClient Client(MockTransport transport) => new(new ApifyClientOptions
    {
        Token = "t",
        MinDelayBetweenRetriesMillis = 1,
        TimeoutSecs = 5,
        HttpTransport = transport,
    });

    private static string[] Issues(string method, string path, string body)
        => ResponseSchemas.Validate(method, path, JsonNode.Parse(body)).ToArray();

    [Fact]
    public void EmbeddedSchemasMatchTheClientsSpecVersion()
    {
        Assert.Equal(ApifyClientVersion.ApiSpecVersion, ResponseSchemas.SpecVersion);
    }

    [Fact]
    public void ValidResponseHasNoIssues()
    {
        Assert.Empty(Issues("GET", "/v2/actor-builds/b1", "{\"data\":{" + Build + "}}"));
    }

    [Fact]
    public void MissingRequiredPropertyIsReportedWithItsLocation()
    {
        var issues = Issues("GET", "/v2/actor-builds/b1", "{\"data\":{\"id\":\"b\"}}");

        Assert.Contains("$.data.actId: required property is missing", issues);
        Assert.Contains("$.data.status: required property is missing", issues);
    }

    [Fact]
    public void WrongJsonTypeIsReported()
    {
        var issues = Issues("GET", "/v2/actor-builds/b1", "{\"data\":{" + Build.Replace("\"id\":\"b\"", "\"id\":5", StringComparison.Ordinal) + "}}");

        Assert.Equal(new[] { "$.data.id: expected string but got number" }, issues);
    }

    [Fact]
    public void NullIsAcceptedWhereTheSpecAllowsIt()
    {
        // The spec lets a build's actVersion.buildTag be null (apify-docs #3093).
        Assert.Empty(Issues("GET", "/v2/actor-builds/b1", "{\"data\":{" + Build + ",\"actVersion\":{\"buildTag\":null}}}"));
    }

    [Fact]
    public void ScheduleUiStateMayBeNullAlthoughTheSpecTypesItAsAnObject()
    {
        // The API returns uiState as null once an update changed the cron expression or timezone and the
        // Console editor state was dropped; the generator widens the spec for it (NULLABLE_OVERRIDES).
        var schedule = SpecFixtures.ScheduleItem("s1").Replace("\"actions\":[]", "\"actions\":[],\"uiState\":null", StringComparison.Ordinal);

        Assert.Empty(Issues("GET", "/v2/schedules/s1", "{\"data\":" + schedule + "}"));
    }

    [Fact]
    public void ScheduleUiStateStillHasToBeAnObjectWhenPresent()
    {
        var schedule = SpecFixtures.ScheduleItem("s1").Replace("\"actions\":[]", "\"actions\":[],\"uiState\":5", StringComparison.Ordinal);

        Assert.Contains(Issues("GET", "/v2/schedules/s1", "{\"data\":" + schedule + "}"), issue => issue.StartsWith("$.data.uiState:", StringComparison.Ordinal));
    }

    [Fact]
    public void NullIsRejectedWhereTheSpecDoesNotAllowIt()
    {
        var issues = Issues("GET", "/v2/actor-builds/b1", "{\"data\":{" + Build + ",\"actVersion\":{\"versionNumber\":null}}}");

        Assert.Single(issues);
        Assert.Contains("actVersion.versionNumber", issues[0], StringComparison.Ordinal);
    }

    [Fact]
    public void DateTimeFormatIsEnforced()
    {
        var issues = Issues("GET", "/v2/actor-builds/b1", "{\"data\":{" + Build.Replace("2026-01-01T00:00:00.000Z", "yesterday", StringComparison.Ordinal) + "}}");

        Assert.Equal(new[] { "$.data.startedAt: is not a valid date-time value" }, issues);
    }

    [Fact]
    public void PatternIsEnforced()
    {
        var issues = Issues("GET", "/v2/actor-builds/b1", "{\"data\":{" + Build.Replace("0.0.1", "not-a-version", StringComparison.Ordinal) + "}}");

        Assert.Single(issues);
        Assert.Contains("$.data.buildNumber: does not match the pattern", issues[0], StringComparison.Ordinal);
    }

    [Fact]
    public void NumericBoundsAreEnforced()
    {
        var below = Issues("GET", "/v2/actor-runs/r1", "{\"data\":" + SpecFixtures.RunObject("r").Replace("\"memoryMbytes\":256", "\"memoryMbytes\":64", StringComparison.Ordinal) + "}");
        var above = Issues("GET", "/v2/actor-runs/r1", "{\"data\":" + SpecFixtures.RunObject("r").Replace("\"memoryMbytes\":256", "\"memoryMbytes\":999999", StringComparison.Ordinal) + "}");

        Assert.Equal(new[] { "$.data.options.memoryMbytes: is a value outside the documented range (minimum 128)" }, below);
        Assert.Equal(new[] { "$.data.options.memoryMbytes: is a value outside the documented range (maximum 32768)" }, above);
    }

    [Fact]
    public void EmailIsOnlyRequiredToBeAString()
    {
        // Like the reference client's schemas: an address such as user@localhost is not rejected.
        Assert.Empty(Issues("GET", "/v2/users/me", "{\"data\":{\"username\":\"u\",\"email\":\"user@localhost\"}}"));
    }

    [Fact]
    public void UriFormatIsEnforced()
    {
        var issues = Issues("GET", "/v2/key-value-stores/s1/keys", SpecFixtures.KeysPage(1, false, null, "a").Replace("https://example.com/a", "not a url", StringComparison.Ordinal));

        Assert.Equal(new[] { "$.data.items[0].recordPublicUrl: is not a valid uri value" }, issues);
    }

    [Fact]
    public async Task ExceptionExposesTheIssuesAsAStructuredList()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"b\"}}");

        var ex = await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Build("b1").GetAsync());

        Assert.Contains("$.data.actId: required property is missing", ex.Issues);
        Assert.Contains(ex.Issues, static issue => issue.StartsWith("$.data.status", StringComparison.Ordinal));
        Assert.StartsWith("response does not match the API specification: ", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownPropertiesAndUnknownEnumValuesAreAccepted()
    {
        Assert.Empty(Issues("GET", "/v2/actor-builds/b1", "{\"data\":{" + Build.Replace("SUCCEEDED", "SOME-FUTURE-STATUS", StringComparison.Ordinal) + ",\"brandNewField\":{\"x\":1}}}"));
    }

    [Fact]
    public void UndocumentedOperationIsNotValidated()
    {
        Assert.Empty(Issues("GET", "/v2/not-a-documented-endpoint", "[]"));
    }

    [Fact]
    public void TheMostSpecificPathTemplateWins()
    {
        // `/runs/last` must be matched by its own template, not by `/runs/{runId}`: both are valid runs, so
        // the way to tell them apart is that an invalid body is still reported for either.
        Assert.NotEmpty(Issues("GET", "/v2/actors/a1/runs/last?status=SUCCEEDED", "{\"data\":{}}"));
        Assert.NotEmpty(Issues("GET", "/v2/actor-runs/r1", "{\"data\":{}}"));
    }

    [Fact]
    public async Task GetWithArrayDataThrowsInsteadOfLookingLikeNotFound()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":[]}");

        var ex = await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Actor("me~act").GetAsync());

        Assert.Equal("GET", ex.HttpMethod);
    }

    [Fact]
    public async Task ListWithArrayDataThrows()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":[]}");

        await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Actors().ListAsync());
    }

    [Fact]
    public async Task ResourceMissingRequiredPropertiesThrowsWithTheIssuesInTheMessage()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{\"id\":\"b\"}}");

        var ex = await Assert.ThrowsAsync<ResponseValidationException>(() => Client(transport).Build("b1").GetAsync());

        Assert.Contains("$.data.actId: required property is missing", ex.Message, StringComparison.Ordinal);
        Assert.EndsWith("/actor-builds/b1", ex.Path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidResourceIsReturned()
    {
        var transport = new MockTransport().QueueResponse(200, "{\"data\":{" + Build + "}}");

        var build = await Client(transport).Build("b1").GetAsync();

        Assert.Equal("b", build!.Id);
    }
}
