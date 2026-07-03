using System;
using System.Threading.Tasks;
using Apify.Client;
using Apify.Client.Options;

namespace Apify.Client.Tests.Examples;

/// <summary>Create a new Actor, build it, run it, wait, and print the finished run log.</summary>
public static class CreateBuildRunActorExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        var suffix = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var created = await client.Actors().CreateAsync(new
        {
            name = "dotnet-example-actor-" + suffix,
            isPublic = false,
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
                        new { name = "main.js", format = "TEXT", content = "console.log('hi');" },
                    },
                },
            },
        });

        try
        {
            var build = await client.Actor(created.Id!).BuildAsync("0.0", new ActorBuildOptions());
            await client.Build(build.Id!).WaitForFinishAsync(300);
            var run = await client.Actor(created.Id!).CallAsync(null, null, 120);
            var log = await client.Run(run.Id!).Log().GetAsync();
            if (log is not null)
            {
                Console.WriteLine(log);
            }
        }
        finally
        {
            await client.Actor(created.Id!).DeleteAsync();
        }
    }
}
