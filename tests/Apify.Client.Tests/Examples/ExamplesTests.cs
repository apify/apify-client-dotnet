using System;
using System.Threading.Tasks;
using Apify.Client;
using Xunit;

namespace Apify.Client.Tests.Examples;

/// <summary>
/// Runs each documentation example end-to-end against the live API, proving the snippets in the docs
/// actually work. Skipped when <c>APIFY_TOKEN</c> is not set. This is the "Test examples" CI step.
/// </summary>
[Trait("Category", "Examples")]
public sealed class ExamplesTests
{
    [SkippableFact]
    public Task RunStoreActor() => RunStoreActorExample.RunAsync(Client());

    [SkippableFact]
    public Task Storages() => StoragesExample.RunAsync(Client());

    [SkippableFact]
    public Task GetAccount() => GetAccountExample.RunAsync(Client());

    [SkippableFact]
    public Task CreateBuildRunActor() => CreateBuildRunActorExample.RunAsync(Client());

    [SkippableFact]
    public Task RunAndLastRunStorages() => RunAndLastRunStoragesExample.RunAsync(Client());

    [SkippableFact]
    public Task IterateStore() => IterateStoreExample.RunAsync(Client());

    [SkippableFact]
    public Task LogRedirection() => LogRedirectionExample.RunAsync(Client());

    private static ApifyClient Client()
    {
        var token = Environment.GetEnvironmentVariable("APIFY_TOKEN");
        Skip.If(string.IsNullOrEmpty(token), "skipping: APIFY_TOKEN is not set");

        var apiUrl = Environment.GetEnvironmentVariable("APIFY_API_URL");
        var baseUrl = string.IsNullOrEmpty(apiUrl) ? ApifyClient.DefaultBaseUrl : apiUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/v2", StringComparison.Ordinal))
        {
            baseUrl = baseUrl[..^"/v2".Length];
        }

        return new ApifyClient(new ApifyClientOptions { Token = token, BaseUrl = baseUrl });
    }
}
