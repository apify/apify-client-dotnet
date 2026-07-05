using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>An Actor on the Apify platform.</summary>
public sealed class Actor : ApifyResource
{
    /// <summary>Wraps a raw Actor object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public Actor(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique Actor ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The ID of the user who owns the Actor.</summary>
    public string? UserId => GetString("userId");

    /// <summary>The technical name of the Actor (used in API paths).</summary>
    public string? Name => GetString("name");

    /// <summary>The username of the Actor's owner.</summary>
    public string? Username => GetString("username");

    /// <summary>The human-readable title shown in the UI.</summary>
    public string? Title => GetString("title");

    /// <summary>A description of what the Actor does.</summary>
    public string? Description => GetString("description");

    /// <summary>Whether the Actor is publicly available in Apify Store.</summary>
    public bool? IsPublic => GetBool("isPublic");

    /// <summary>When the Actor was created (ISO-8601 string).</summary>
    public string? CreatedAt => GetString("createdAt");

    /// <summary>When the Actor was last modified (ISO-8601 string).</summary>
    public string? ModifiedAt => GetString("modifiedAt");
}
