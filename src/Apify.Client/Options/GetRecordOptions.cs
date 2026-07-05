using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures fetching a key-value-store record.</summary>
public sealed class GetRecordOptions
{
    /// <summary>Controls the <c>Content-Disposition: attachment</c> behaviour.</summary>
    public bool? Attachment { get; init; }

    /// <summary>A pre-shared URL signature granting access without an API token.</summary>
    public string? Signature { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddBool("attachment", Attachment).AddString("signature", Signature);
    }
}
