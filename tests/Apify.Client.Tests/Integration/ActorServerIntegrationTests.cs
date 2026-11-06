using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Options;
using Xunit;

namespace Apify.Client.Tests.Integration;

/// <summary>
/// The Server Actor client against a temporary Server Actor that echoes every request it receives.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ActorServerIntegrationTests : IntegrationTestBase
{
    /// <summary>How long to wait for the echo Actor's build to finish, in seconds.</summary>
    private const int BuildWaitSecs = 300;

    private const string OpenApiSpec = """{"openapi":"3.0.0","info":{"title":"Echo server","version":"1.0.0"},"paths":{"/echo":{"post":{"responses":{"200":{"description":"The echoed request."}}}}}}""";

    private const string Dockerfile = "FROM node:22-alpine\nCOPY main.js ./\nCMD [\"node\", \"main.js\"]\n";

    /// <summary>
    /// A web server that answers every request, including the platform's readiness probe, with the request it
    /// received, under the status in the <c>status</c> query parameter, or 200.
    /// </summary>
    private const string EchoServerSource = """
        const http = require('http');

        http.createServer((req, res) => {
            let body = '';
            req.on('data', (chunk) => { body += chunk; });
            req.on('end', () => {
                const status = Number(new URL(req.url, 'http://localhost').searchParams.get('status')) || 200;
                res.writeHead(status, { 'Content-Type': 'application/json' });
                res.end(JSON.stringify({ method: req.method, url: req.url, headers: req.headers, body }));
            });
        }).listen(process.env.ACTOR_WEB_SERVER_PORT);
        """;

    private static object EchoServerActor(string name)
    {
        var actorJson = new JsonObject
        {
            ["actorSpecification"] = 1,
            ["name"] = name,
            ["version"] = "0.0",
            ["usesStandbyMode"] = true,
            ["webServerSchema"] = JsonNode.Parse(OpenApiSpec),
        };
        return new
        {
            name,
            versions = new[]
            {
                new
                {
                    versionNumber = "0.0",
                    sourceType = "SOURCE_FILES",
                    buildTag = "latest",
                    sourceFiles = new object[]
                    {
                        new { name = "Dockerfile", format = "TEXT", content = Dockerfile },
                        new { name = "main.js", format = "TEXT", content = EchoServerSource },
                        new { name = ".actor/actor.json", format = "TEXT", content = actorJson.ToJsonString() },
                    },
                },
            },
            actorStandby = new { isEnabled = true, memoryMbytes = 256, idleTimeoutSecs = 60 },
        };
    }

    [SkippableFact]
    public async Task ServerClientReturnsTheUrlAndSpecificationAndSendsRequestsToTheServer()
    {
        var client = RequireClient();
        var created = await client.Actors().CreateAsync(EchoServerActor(UniqueName("server")));
        try
        {
            var build = await client.Actor(created.Id!).BuildAsync("0.0");
            var finished = await client.Build(build.Id!).WaitForFinishAsync(BuildWaitSecs);
            Assert.Equal("SUCCEEDED", finished.Status);

            var actor = await client.Actor(created.Id!).GetAsync();
            var server = client.Actor(created.Id!).Server();

            Assert.Equal(actor!.StandbyUrl, await server.GetUrlAsync());
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(OpenApiSpec), await server.GetOpenApiSpecAsync()));

            using var response = await server.RequestAsync(
                "/echo",
                new ActorServerRequestOptions
                {
                    Method = HttpMethod.Post,
                    Query = new Dictionary<string, object?> { ["q"] = "apify" },
                    Body = new { limit = 5 },
                });
            var echoed = (await response.ReadAsJsonAsync())!;

            Assert.Equal(200, response.StatusCode);
            Assert.Equal("POST", echoed["method"]!.GetValue<string>());
            Assert.Equal("/echo?q=apify", echoed["url"]!.GetValue<string>());
            Assert.Equal(5, JsonNode.Parse(echoed["body"]!.GetValue<string>())!["limit"]!.GetValue<int>());
            // The platform consumes the client's token, so it never reaches the Actor's code.
            Assert.Null(echoed["headers"]!["x-apify-authorization"]);
            Assert.Null(echoed["headers"]!["authorization"]);

            using var withOwnAuthorization = await server.RequestAsync(
                "/echo",
                new ActorServerRequestOptions { Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer actor-token" } });
            var echoedHeaders = (await withOwnAuthorization.ReadAsJsonAsync())!["headers"]!;
            Assert.Equal("Bearer actor-token", echoedHeaders["authorization"]!.GetValue<string>());

            using var errorResponse = await server.RequestAsync(
                "/echo",
                new ActorServerRequestOptions { Query = new Dictionary<string, object?> { ["status"] = 404 } });
            Assert.Equal(404, errorResponse.StatusCode);
            Assert.Equal("/echo?status=404", (await errorResponse.ReadAsJsonAsync())!["url"]!.GetValue<string>());
        }
        finally
        {
            await client.Actor(created.Id!).DeleteAsync();
        }
    }

    [SkippableFact]
    public async Task ServerClientRejectsAnActorThatIsNotAServerActor()
    {
        var client = RequireClient();
        var created = await client.Actors().CreateAsync(MinimalActor(UniqueName("not-server")));
        try
        {
            await Assert.ThrowsAsync<ActorServerException>(() => client.Actor(created.Id!).Server().GetUrlAsync());
        }
        finally
        {
            await client.Actor(created.Id!).DeleteAsync();
        }
    }
}
