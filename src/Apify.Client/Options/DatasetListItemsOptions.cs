using System.Collections.Generic;
using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>
/// Configures listing or downloading dataset items (<c>GET /v2/datasets/{datasetId}/items</c>). All
/// fields are optional.
/// </summary>
public sealed class DatasetListItemsOptions
{
    /// <summary>Number of items to skip.</summary>
    public int? Offset { get; init; }

    /// <summary>Maximum number of items to return.</summary>
    public int? Limit { get; init; }

    /// <summary>Return items newest-first.</summary>
    public bool? Desc { get; init; }

    /// <summary>Restrict the output to these fields.</summary>
    public IReadOnlyList<string>? Fields { get; init; }

    /// <summary>Positionally rename the selected <see cref="Fields"/> (requires <see cref="Fields"/>).</summary>
    public IReadOnlyList<string>? OutputFields { get; init; }

    /// <summary>Exclude these fields from the output.</summary>
    public IReadOnlyList<string>? Omit { get; init; }

    /// <summary>Skip empty items.</summary>
    public bool? SkipEmpty { get; init; }

    /// <summary>Skip hidden fields (those starting with <c>#</c>).</summary>
    public bool? SkipHidden { get; init; }

    /// <summary>Return only clean (non-empty, non-hidden) items.</summary>
    public bool? Clean { get; init; }

    /// <summary>Expand these fields (each array element becomes a separate item).</summary>
    public IReadOnlyList<string>? Unwind { get; init; }

    /// <summary>Flatten these nested fields into dot-notation keys.</summary>
    public IReadOnlyList<string>? Flatten { get; init; }

    /// <summary>Select a predefined dataset view for field selection.</summary>
    public string? View { get; init; }

    /// <summary>Return simplified (flattened, cleaned) items.</summary>
    public bool? Simplified { get; init; }

    /// <summary>Skip items that come from failed pages.</summary>
    public bool? SkipFailedPages { get; init; }

    /// <summary>A pre-shared URL signature granting access without an API token.</summary>
    public string? Signature { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddInt("offset", Offset)
            .AddInt("limit", Limit)
            .AddBool("desc", Desc)
            .AddCsv("fields", Fields)
            .AddCsv("outputFields", OutputFields)
            .AddCsv("omit", Omit)
            .AddBool("skipEmpty", SkipEmpty)
            .AddBool("skipHidden", SkipHidden)
            .AddBool("clean", Clean)
            .AddCsv("unwind", Unwind)
            .AddCsv("flatten", Flatten)
            .AddString("view", View)
            .AddBool("simplified", Simplified)
            .AddBool("skipFailedPages", SkipFailedPages)
            .AddString("signature", Signature);
    }
}
