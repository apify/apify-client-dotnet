using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>A dataset stores structured results from Actor runs.</summary>
public sealed class Dataset : ApifyResource
{
    /// <summary>Wraps a raw dataset object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public Dataset(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique dataset ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The dataset name (empty for unnamed datasets).</summary>
    public string? Name => GetString("name");

    /// <summary>The ID of the user who owns the dataset.</summary>
    public string? UserId => GetString("userId");

    /// <summary>When the dataset was created (ISO-8601 string).</summary>
    public string? CreatedAt => GetString("createdAt");

    /// <summary>When the dataset was last modified (ISO-8601 string).</summary>
    public string? ModifiedAt => GetString("modifiedAt");

    /// <summary>The number of items currently stored.</summary>
    public long? ItemCount => GetInt("itemCount");
}
