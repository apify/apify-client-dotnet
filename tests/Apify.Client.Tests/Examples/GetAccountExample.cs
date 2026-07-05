using System;
using System.Threading.Tasks;
using Apify.Client;

namespace Apify.Client.Tests.Examples;

/// <summary>Get own account details.</summary>
public static class GetAccountExample
{
    public static async Task RunAsync(ApifyClient client)
    {
        var user = await client.Me().GetAsync();
        if (user is not null)
        {
            Console.WriteLine("Account " + user.Id + " / " + user.Username);
        }
    }
}
