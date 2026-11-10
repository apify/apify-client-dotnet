namespace Apify.Client.Internal;

/// <summary>
/// The timeout tier of a request, as the reference client's <c>TimeoutTier</c>. Each tier's duration is configured on
/// <see cref="Apify.Client.ApifyClientOptions"/>, and each method picks the tier that fits its expected duration.
/// </summary>
internal enum RequestTier
{
    /// <summary>Simple metadata reads and writes (get/update/delete a resource, small sub-resources).</summary>
    Short,

    /// <summary>Listing, batch and trigger calls (collection listing, pushing items, starting a run).</summary>
    Medium,

    /// <summary>Downloads, uploads and streaming (dataset items, key-value store records, logs).</summary>
    Long,
}
