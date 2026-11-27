using System.Collections.Generic;
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

    /// <summary>The full name of the Actor's owner.</summary>
    public string? UserFullName => GetString("userFullName");

    /// <summary>The Actor's description.</summary>
    public string? Description => GetString("description");

    /// <summary>The IDs of the Store categories of the Actor (<c>null</c> if the API reports none).</summary>
    public IReadOnlyList<string>? Categories => GetStringList("categories");

    /// <summary>Whether the Actor's developer has a verified identity.</summary>
    public bool? IsVerified => GetBool("isVerified");

    /// <summary>
    /// The Actor's usage statistics and Store metrics: <c>totalUsers</c>, <c>totalRuns</c>, <c>totalUsers7Days</c>,
    /// <c>totalUsers30Days</c>, <c>totalUsers90Days</c>, <c>actorReviewRating</c>, and <c>actorReviewCount</c>. The Store
    /// endpoint returns this reduced set instead of the full Actor statistics.
    /// </summary>
    public JsonObject? Stats => Get("stats") as JsonObject;
}
