using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>Display configuration of a task's public landing page in Apify Store.</summary>
/// <remarks>
/// The task is published when <see cref="PublishedAt"/> is set. It is read-only; use
/// <see cref="Resources.TaskClient.PublishAsync"/> and <see cref="Resources.TaskClient.UnpublishAsync"/>.
/// </remarks>
public sealed class TaskPublicConfig : ApifyResource
{
    /// <summary>Wraps a raw public-config object.</summary>
    /// <param name="data">The raw decoded object.</param>
    public TaskPublicConfig(JsonObject data)
        : base(data)
    {
    }

    /// <summary>When the task was published (ISO 8601), or <c>null</c> if it is not.</summary>
    public string? PublishedAt => GetString("publishedAt");

    /// <summary>Name to display for search engines such as Google.</summary>
    public string? SeoTitle => GetString("seoTitle");

    /// <summary>Description to display for search engines such as Google.</summary>
    public string? SeoDescription => GetString("seoDescription");

    /// <summary>The category of the task on its public landing page. Not in the OpenAPI schema; kept because the reference client exposes it.</summary>
    public string? Categorization => GetString("categorization");

    /// <summary>The input schema fields shown on the public landing page.</summary>
    public IReadOnlyList<string>? InputSchemaFields => GetStringList("inputSchemaFields");

    /// <summary>The name of the dataset shown on the public landing page.</summary>
    public string? DatasetName => GetString("datasetName");

    /// <summary>The view of the dataset shown on the public landing page.</summary>
    public string? DatasetView => GetString("datasetView");
}
