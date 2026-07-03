using System;
using System.IO;
using System.Threading.Tasks;
using Apify.Client;

namespace Apify.Client.Tests.Examples;

/// <summary>Run an Actor with log redirection turned on (stream the run's log).</summary>
public static class LogRedirectionExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        var run = await client.Actor("apify/hello-world").StartAsync();
        // Wait for the run to finish so the full log is available, then stream it to stdout.
        await client.Run(run.Id!).WaitForFinishAsync(120);
        using var stream = await client.Run(run.Id!).GetStreamedLogAsync();
        using var reader = new StreamReader(stream);
        Console.WriteLine(await reader.ReadToEndAsync());
    }
}
