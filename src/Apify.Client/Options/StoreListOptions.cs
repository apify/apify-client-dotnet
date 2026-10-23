using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Options for listing/iterating the Apify Store (<c>GET /v2/store</c>).</summary>
public sealed class StoreListOptions
{
    /// <summary>Number of Actors to skip.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of Actors to return (when iterating, in total across all pages).</summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Maximum number of items requested per API call when iterating across pages (must be positive). Only
    /// iteration uses it, as the page size; it is never sent to the API. The effective page size is the
    /// smaller of this and the remaining <c>Limit</c>. Defaults to the API's own page size.
    /// </summary>
    public int? ChunkSize { get; init; }

    /// <summary>Full-text search query.</summary>
    public string? Search { get; init; }

    /// <summary>The sort field (e.g. <c>popularity</c>, <c>newest</c>).</summary>
    public string? SortBy { get; init; }

    /// <summary>Filter Actors by category.</summary>
    public string? Category { get; init; }

    /// <summary>Filter Actors by owner username.</summary>
    public string? Username { get; init; }

    /// <summary>
    /// Filter Actors by pricing model (<c>FREE</c>, <c>PRICE_PER_DATASET_ITEM</c>,
    /// <c>PAY_PER_EVENT</c>). The rental model (<c>FLAT_PRICE_PER_MONTH</c>) is no longer supported.
    /// </summary>
    public string? PricingModel { get; init; }

    /// <summary>Include Actors the current user cannot run.</summary>
    public bool? IncludeUnrunnableActors { get; init; }

    /// <summary>Filter to Actors that allow agentic users.</summary>
    public bool? AllowsAgenticUsers { get; init; }

    /// <summary>The response format (<c>full</c>, <c>agent</c>).</summary>
    public string? ResponseFormat { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("offset", Offset)
            .AddInt("limit", Limit)
            .AddString("search", Search)
            .AddString("sortBy", SortBy)
            .AddString("category", Category)
            .AddString("username", Username)
            .AddString("pricingModel", PricingModel)
            .AddBool("includeUnrunnableActors", IncludeUnrunnableActors)
            .AddBool("allowsAgenticUsers", AllowsAgenticUsers)
            .AddString("responseFormat", ResponseFormat);
    }
}
