using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
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
    /// Retry budget for <see cref="FindsAllEventuallyAsync{T}"/>: how many times a freshly created
    /// resource's collection listing is re-scanned before giving up on it having propagated.
    /// </summary>
    private const int EventualConsistencyAttempts = 16;

    /// <summary>
    /// Delay between retries in <see cref="FindsAllEventuallyAsync{T}"/>. Combined with
    /// <see cref="EventualConsistencyAttempts"/>, the total wait budget is
    /// <c>(EventualConsistencyAttempts - 1) * EventualConsistencyBackoff</c> = ~15s.
    /// </summary>
    private static readonly TimeSpan EventualConsistencyBackoff = TimeSpan.FromSeconds(1);

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

    /// <summary>A minimal Actor task definition targeting the public <c>apify/hello-world</c> Actor.</summary>
    protected static object MinimalTask(string name) => new
    {
        actId = "apify/hello-world",
        name,
        options = new { build = "latest", memoryMbytes = 256, timeoutSecs = 60 },
        input = new { message = "hello" },
    };

    /// <summary>A minimal, disabled schedule definition (no actions, so it never actually fires).</summary>
    protected static object MinimalSchedule(string name) => new
    {
        name,
        cronExpression = "0 0 * * *",
        isEnabled = false,
        isExclusive = true,
        actions = Array.Empty<object>(),
    };

    /// <summary>A minimal ad-hoc webhook definition targeting a condition that never actually fires.</summary>
    protected static object MinimalWebhook(string requestUrl) => new
    {
        isAdHoc = true,
        eventTypes = new[] { "ACTOR.RUN.SUCCEEDED" },
        condition = new { actorRunId = "ZZZZZZZZZZZZZZZZZ" },
        requestUrl,
    };

    /// <summary>
    /// Retries <paramref name="check"/> up to <paramref name="attempts"/> times, sleeping
    /// <paramref name="backoff"/> between attempts, until it returns <c>true</c>. Used to tolerate
    /// collection-listing eventual consistency: a resource created through a write endpoint is not always
    /// immediately reflected in that collection's LIST response.
    /// </summary>
    protected static async Task<bool> PollUntilAsync(int attempts, TimeSpan backoff, Func<Task<bool>> check)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await check().ConfigureAwait(false))
            {
                return true;
            }

            if (attempt < attempts - 1)
            {
                await Task.Delay(backoff).ConfigureAwait(false);
            }
        }

        return false;
    }

    /// <summary>
    /// Drains <paramref name="items"/>, removing each item's id (via <paramref name="idOf"/>) from a copy of
    /// <paramref name="targetIds"/>, stopping as soon as every target has been seen (or the sequence
    /// completes). <paramref name="safetyLimit"/> bounds the scan purely as a safety net against an
    /// unbounded sequence; no test in this suite scans anywhere near that many items.
    /// </summary>
    private static async Task<bool> FindsAllAsync<T>(IAsyncEnumerable<T> items, Func<T, string> idOf, IReadOnlySet<string> targetIds, int safetyLimit)
    {
        var remaining = new HashSet<string>(targetIds);
        var scanned = 0;
        await foreach (var item in items)
        {
            remaining.Remove(idOf(item));
            if (remaining.Count == 0 || ++scanned >= safetyLimit)
            {
                break;
            }
        }

        return remaining.Count == 0;
    }

    /// <summary>
    /// Asserts that iterating a freshly-built sequence (via <paramref name="newSequence"/>, called again on
    /// every retry) eventually yields every id in <paramref name="targetIds"/>, tolerating collection-listing
    /// eventual consistency (see <see cref="PollUntilAsync"/>). An already-consistent account matches on the
    /// first pass with no sleeping.
    /// </summary>
    protected static Task<bool> FindsAllEventuallyAsync<T>(
        Func<IAsyncEnumerable<T>> newSequence,
        Func<T, string> idOf,
        IReadOnlySet<string> targetIds,
        int safetyLimit = 10_000)
        => PollUntilAsync(EventualConsistencyAttempts, EventualConsistencyBackoff, () => FindsAllAsync(newSequence(), idOf, targetIds, safetyLimit));
}
