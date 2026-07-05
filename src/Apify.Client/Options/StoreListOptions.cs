using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Options for listing/iterating the Apify Store (<c>GET /v2/store</c>).</summary>
public sealed class StoreListOptions
{
    /// <summary>Number of Actors to skip.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of Actors to return (also the per-page size when iterating).</summary>
    public int? Limit { get; init; }

    /// <summary>Full-text search query.</summary>
    public string? Search { get; init; }

    /// <summary>The sort field (e.g. <c>popularity</c>, <c>newest</c>).</summary>
    public string? SortBy { get; init; }

    /// <summary>Filter Actors by category.</summary>
    public string? Category { get; init; }

    /// <summary>Filter Actors by owner username.</summary>
    public string? Username { get; init; }

    /// <summary>
    /// Filter Actors by pricing model (<c>FREE</c>, <c>FLAT_PRICE_PER_MONTH</c>,
    /// <c>PRICE_PER_DATASET_ITEM</c>, <c>PAY_PER_EVENT</c>).
    /// </summary>
    public string? PricingModel { get; init; }

    /// <summary>Include Actors the current user cannot run.</summary>
    public bool? IncludeUnrunnableActors { get; init; }

    /// <summary>Filter to Actors that allow agentic users.</summary>
    public bool? AllowsAgenticUsers { get; init; }

    /// <summary>The response format (<c>full</c>, <c>agent</c>).</summary>
    public string? ResponseFormat { get; init; }

    /// <summary>Returns a copy of these options with a new <see cref="Offset"/> (used by lazy iteration).</summary>
    internal StoreListOptions WithOffset(int? offset) => new()
    {
        Offset = offset,
        Limit = Limit,
        Search = Search,
        SortBy = SortBy,
        Category = Category,
        Username = Username,
        PricingModel = PricingModel,
        IncludeUnrunnableActors = IncludeUnrunnableActors,
        AllowsAgenticUsers = AllowsAgenticUsers,
        ResponseFormat = ResponseFormat,
    };

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
