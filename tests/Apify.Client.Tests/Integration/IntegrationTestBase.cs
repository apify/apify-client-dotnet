using System;
using System.Security.Cryptography;
using Apify.Client;
using Xunit;

namespace Apify.Client.Tests.Integration;

/// <summary>
/// Shared setup for the integration test suite.
/// </summary>
/// <remarks>
/// All integration tests require a valid <c>APIFY_TOKEN</c> for the test account. The API base URL is
/// taken from <c>APIFY_API_URL</c> (which includes the <c>/v2</c> suffix) and falls back to
/// <c>https://api.apify.com/v2</c>. Tests are designed to run concurrently — including against the same
/// test account from several language clients at once — so every test creates uniquely-named resources
/// and cleans them up.
/// </remarks>
public abstract class IntegrationTestBase
{
    /// <summary>The integration-test contract fallback base URL.</summary>
    private const string DefaultApiUrl = "https://api.apify.com/v2";

    /// <summary>
    /// Derives the client base URL from an optional <c>APIFY_API_URL</c>. The variable includes the
    /// <c>/v2</c> suffix (per the integration-test contract) and falls back to the default. Since the client
    /// appends <c>/v2</c> itself, the suffix is stripped here.
    /// </summary>
    protected static string ResolveBaseUrl(string? apiUrl)
    {
        if (string.IsNullOrEmpty(apiUrl))
        {
            apiUrl = DefaultApiUrl;
        }

        var trimmed = apiUrl.TrimEnd('/');
        if (trimmed.EndsWith("/v2", StringComparison.Ordinal))
        {
            trimmed = trimmed[..^"/v2".Length];
        }

        return trimmed;
    }

    /// <summary>Returns a configured client, or skips the test if <c>APIFY_TOKEN</c> is unset.</summary>
    protected static ApifyClient RequireClient()
    {
        var token = Environment.GetEnvironmentVariable("APIFY_TOKEN");
        Skip.If(string.IsNullOrEmpty(token), "skipping: APIFY_TOKEN is not set");

        var apiUrl = Environment.GetEnvironmentVariable("APIFY_API_URL");
        return new ApifyClient(new ApifyClientOptions { Token = token, BaseUrl = ResolveBaseUrl(apiUrl) });
    }

    /// <summary>
    /// Generates a collision-resistant resource name for test isolation. The random component lets the same
    /// test run in parallel (across processes and languages) without clobbering shared state.
    /// </summary>
    protected static string UniqueName(string prefix)
        => "dotnet-test-" + prefix + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();

    /// <summary>A minimal Actor definition; the API requires at least one version.</summary>
    protected static object MinimalActor(string name) => new
    {
        name,
        isPublic = false,
        description = "Integration test actor",
        versions = new[]
        {
            new
            {
                versionNumber = "0.0",
                sourceType = "SOURCE_FILES",
                buildTag = "latest",
                sourceFiles = new object[]
                {
                    new { name = "Dockerfile", format = "TEXT", content = "FROM apify/actor-node:20\nCOPY . ./\nCMD node main.js" },
                    new { name = "main.js", format = "TEXT", content = "console.log('hello from dotnet client test');" },
                },
            },
        },
    };
}
