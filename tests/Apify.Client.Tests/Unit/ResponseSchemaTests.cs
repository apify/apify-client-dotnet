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
    public void NullIsRejectedWhereTheSpecDoesNotAllowIt()
    {
        var issues = Issues("GET", "/v2/actor-builds/b1", "{\"data\":{" + Build + ",\"actVersion\":{\"versionNumber\":null}}}");

        Assert.Single(issues);
        Assert.Contains("actVersion.versionNumber", issues[0], StringComparison.Ordinal);
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
