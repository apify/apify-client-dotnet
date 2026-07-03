using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>
/// Adds format-specific options for downloading dataset items on top of the shared item
/// filtering/projection options (<see cref="DatasetListItemsOptions"/>).
/// </summary>
public sealed class DatasetDownloadOptions
{
    /// <summary>The shared filtering/projection options.</summary>
    public DatasetListItemsOptions? Items { get; init; }

    /// <summary>Set <c>Content-Disposition: attachment</c> on the response.</summary>
    public bool? Attachment { get; init; }

    /// <summary>Prepend a UTF-8 BOM (useful for Excel-compatible CSV).</summary>
    public bool? Bom { get; init; }

    /// <summary>The CSV field delimiter (default <c>,</c>).</summary>
    public string? Delimiter { get; init; }

    /// <summary>Omit the CSV header row.</summary>
    public bool? SkipHeaderRow { get; init; }

    /// <summary>The name of the root XML element (default <c>items</c>).</summary>
    public string? XmlRoot { get; init; }

    /// <summary>The name of the per-item XML element (default <c>item</c>).</summary>
    public string? XmlRow { get; init; }

    /// <summary>The title used for RSS/Atom feed exports.</summary>
    public string? FeedTitle { get; init; }

    /// <summary>The description used for RSS/Atom feed exports.</summary>
    public string? FeedDescription { get; init; }

    internal void AppendTo(QueryParams q)
    {
        Items?.AppendTo(q);
        q.AddBool("attachment", Attachment)
            .AddBool("bom", Bom)
            .AddString("delimiter", Delimiter)
            .AddBool("skipHeaderRow", SkipHeaderRow)
            .AddString("xmlRoot", XmlRoot)
            .AddString("xmlRow", XmlRow)
            .AddString("feedTitle", FeedTitle)
            .AddString("feedDescription", FeedDescription);
    }
}
