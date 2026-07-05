using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>
/// An Apify user account. Private account details for <c>me</c> (email, plan, proxy settings, …) are
/// available via <see cref="ApifyResource.ToJsonObject"/>.
/// </summary>
public sealed class User : ApifyResource
{
    /// <summary>Wraps a raw user object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public User(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique user ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The user's username.</summary>
    public string? Username => GetString("username");
}
