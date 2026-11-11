using System;
using System.Threading.Tasks;
using Apify.Client;

namespace Apify.Client.Tests.Examples;

/// <summary>Run an Actor with log redirection turned on: the run's live log is forwarded to a sink.</summary>
public static class LogRedirectionExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        // The `log` argument redirects the run's live log to the sink (here, stdout) while waiting.
        await client.Actor("apify/hello-world").CallAsync(null, null, 120, log: Console.WriteLine);
    }
}
