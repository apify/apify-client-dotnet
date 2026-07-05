using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>An Actor as listed in the Apify Store.</summary>
public sealed class ActorStoreListItem : ApifyResource
{
    /// <summary>Wraps a raw store-list item.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public ActorStoreListItem(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique Actor ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The technical name of the Actor.</summary>
    public string? Name => GetString("name");

    /// <summary>The username of the Actor's owner.</summary>
    public string? Username => GetString("username");

    /// <summary>The human-readable title.</summary>
    public string? Title => GetString("title");
}
