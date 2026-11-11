using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Apify.Client;
using Xunit;

namespace Apify.Client.Tests.Integration;

/// <summary>Shared setup of the integration tests.</summary>
/// <remarks>
/// The tests need a valid <c>APIFY_TOKEN</c>. The API URL is <c>APIFY_API_URL</c> (with the <c>/v2</c> suffix), or
/// <c>https://api.apify.com/v2</c>. Tests may run concurrently on the same account, including from other language
/// clients, so each creates uniquely named resources and cleans them up.
/// </remarks>
public abstract class IntegrationTestBase
{
    /// <summary>The default base URL.</summary>
    private const string DefaultApiUrl = "https://api.apify.com/v2";

    /// <summary>How many times <see cref="FindsAllEventuallyAsync{T}"/> re-scans a listing for a freshly created resource.</summary>
    protected const int EventualConsistencyAttempts = 31;

    /// <summary>
    /// Delay between the retries of <see cref="FindsAllEventuallyAsync{T}"/>. With <see cref="EventualConsistencyAttempts"/>
    /// the budget is about 90 seconds, the lag listings can have, since they read from database secondaries.
    /// </summary>
    protected static readonly TimeSpan EventualConsistencyBackoff = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Derives the client base URL from <c>APIFY_API_URL</c>, which includes <c>/v2</c>. The suffix is stripped, since the
    /// client appends it.
    /// </summary>
    private static string ResolveBaseUrl(string? apiUrl)
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
    /// <param name="configure">Optional changes to the client options.</param>
    protected static ApifyClient RequireClient(Action<ApifyClientOptions>? configure = null)
    {
        var token = Environment.GetEnvironmentVariable("APIFY_TOKEN");
        Skip.If(string.IsNullOrEmpty(token), "skipping: APIFY_TOKEN is not set");

        var apiUrl = Environment.GetEnvironmentVariable("APIFY_API_URL");
        var options = new ApifyClientOptions { Token = token, BaseUrl = ResolveBaseUrl(apiUrl) };
        configure?.Invoke(options);
        return new ApifyClient(options);
    }

    /// <summary>Generates a unique resource name, so the same test can run in parallel without clobbering shared state.</summary>
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

    /// <summary>
    /// A minimal webhook definition for runs of the given Actor. The condition needs the Actor's ID, not its slug (see
    /// <see cref="ResolveHelloWorldActorIdAsync"/>).
    /// </summary>
    protected static object MinimalWebhook(string requestUrl, string actorId) => new
    {
        eventTypes = new[] { "ACTOR.RUN.SUCCEEDED" },
        condition = new { actorId },
        requestUrl,
    };

    /// <summary>Resolves the ID of the public <c>apify/hello-world</c> Actor, for use as a webhook condition.</summary>
    protected static async Task<string> ResolveHelloWorldActorIdAsync(ApifyClient client)
        => (await client.Actor("apify/hello-world").GetAsync())!.Id!;

    /// <summary>
    /// Retries <paramref name="check"/> up to <paramref name="attempts"/> times with <paramref name="backoff"/> between
    /// them until it returns <c>true</c>, to tolerate listings that do not show a new resource at once.
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
    /// Drains <paramref name="items"/>, removing each item's id (<paramref name="idOf"/>) from a copy of
    /// <paramref name="targetIds"/>, until every target is seen or the sequence ends. <paramref name="safetyLimit"/> only
    /// guards against an unbounded sequence.
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
    /// Asserts that a sequence built by <paramref name="newSequence"/> (called again on every retry) eventually yields every
    /// id in <paramref name="targetIds"/>, tolerating listing lag (see <see cref="PollUntilAsync"/>).
    /// </summary>
    protected static Task<bool> FindsAllEventuallyAsync<T>(
        Func<IAsyncEnumerable<T>> newSequence,
        Func<T, string> idOf,
        IReadOnlySet<string> targetIds,
        int safetyLimit = 10_000)
        => PollUntilAsync(EventualConsistencyAttempts, EventualConsistencyBackoff, () => FindsAllAsync(newSequence(), idOf, targetIds, safetyLimit));
}
