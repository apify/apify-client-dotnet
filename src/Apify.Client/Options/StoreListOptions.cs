using System;
using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Options for listing/iterating the Apify Store (<c>GET /v2/store</c>).</summary>
public sealed class StoreListOptions
{
    // Backs the deprecated SortBy, which AppendTo still sends because the API accepts it for backward compatibility.
    private readonly string? _sortBy;

    /// <summary>Number of Actors to skip. The API ignores it when <see cref="Search"/> is used.</summary>
    public int? Offset { get; init; }

    /// <summary>
    /// Maximum number of Actors to return (when iterating, in total across all pages). The API's default and maximum page
    /// size is 1000, or 100 when <see cref="Search"/> is used, and it reduces a larger value to that.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Maximum items requested per API call when iterating, which must be positive. It sizes the pages and is never sent to
    /// the API; the page size is the smaller of this and the remaining <c>Limit</c>. Defaults to the API's page size. A
    /// <see cref="Search"/> iteration is a single request and does not use it.
    /// </summary>
    public int? ChunkSize { get; init; }

    /// <summary>
    /// Search query. The API ranks the results by relevance and returns a single page of at most 100 Actors, so there is no
    /// second page. Narrow the results with <see cref="Category"/>, <see cref="Username"/>, or <see cref="PricingModel"/>.
    /// </summary>
    public string? Search { get; init; }

    /// <summary>
    /// Deprecated and ignored by the API, which accepts the value for backward compatibility only. Results are ordered by
    /// relevance when <see cref="Search"/> is used, and by Store position otherwise. Sort the results on the client for a
    /// different order.
    /// </summary>
    [Obsolete("The API ignores sortBy. Results are ordered by relevance when Search is used, and by Store position otherwise.")]
    public string? SortBy
    {
        get => _sortBy;
        init => _sortBy = value;
    }

    /// <summary>
    /// Filter Actors by category. Accepts one or more category IDs separated by commas, case-insensitive, such as
    /// <c>AI_AND_ML,ECOMMERCE</c>, and an Actor has to match all of them. An unknown category fails with a <c>400</c> error.
    /// </summary>
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

    /// <summary>
    /// Include each Actor's <c>inputSchema</c>, a compact form of the input schema of its default build (property types and
    /// required properties). The API rejects a <c>limit</c> above <see cref="MaxPageLimitWithInputSchema"/> with a <c>400</c>
    /// error, so a call with a larger <c>Limit</c> fails, and iteration requests pages of at most that size.
    /// </summary>
    public bool? IncludeInputSchema { get; init; }

    /// <summary>The API's maximum page size while <see cref="IncludeInputSchema"/> is set.</summary>
    public const int MaxPageLimitWithInputSchema = 10;

    /// <summary>The response format (<c>full</c>, <c>agent</c>).</summary>
    public string? ResponseFormat { get; init; }

    /// <summary>The page size iteration requests: the caller's <see cref="ChunkSize"/>, capped at the API's maximum while the input schema is included.</summary>
    internal int? IterationChunkSize()
        => IncludeInputSchema is true ? Math.Min(ChunkSize ?? MaxPageLimitWithInputSchema, MaxPageLimitWithInputSchema) : ChunkSize;

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("offset", Offset)
            .AddInt("limit", Limit)
            .AddString("search", Search)
            .AddString("sortBy", _sortBy)
            .AddString("category", Category)
            .AddString("username", Username)
            .AddString("pricingModel", PricingModel)
            .AddBool("includeUnrunnableActors", IncludeUnrunnableActors)
            .AddBool("allowsAgenticUsers", AllowsAgenticUsers)
            .AddBool("includeInputSchema", IncludeInputSchema)
            .AddString("responseFormat", ResponseFormat);
    }
}
