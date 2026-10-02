namespace Apify.Client.Internal;

/// <summary>
/// The timeout tier a request falls into, matching the reference client's <c>TimeoutTier</c>
/// (<c>'short'</c>/<c>'medium'</c>/<c>'long'</c>): each tier's duration is configured once on
/// <see cref="Apify.Client.ApifyClientOptions"/> and every method that sends a request picks the tier that
/// fits its expected duration, so the default wait before giving up is consistent across the client instead
/// of a single one-size-fits-all budget.
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
