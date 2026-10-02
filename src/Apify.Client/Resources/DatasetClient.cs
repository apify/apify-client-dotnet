using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for a specific dataset (and run-nested variants).</summary>
public sealed class DatasetClient
{
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;

    private DatasetClient(HttpClientCore http, ResourceContext ctx)
    {
        _http = http;
        _ctx = ctx;
    }

    internal static DatasetClient ForId(HttpClientCore http, string baseUrl, string id)
        => new(http, ResourceContext.Single(http, baseUrl, "datasets", id));

    internal static DatasetClient Nested(HttpClientCore http, string baseUrl, string subPath, QueryParams? inheritedParams = null)
        => new(http, ResourceContext.NestedSingleton(http, baseUrl, subPath, inheritedParams));

    internal DatasetClient WithPublicBase(string publicBaseUrl)
    {
        _ctx.WithPublicOrigin(publicBaseUrl);
        return this;
    }

    /// <summary>Fetches the dataset metadata, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Dataset?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new Dataset(obj) : null;
    }

    /// <summary>Updates the dataset metadata (e.g. name, title) and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Dataset> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new Dataset(await _ctx.UpdateResourceAsync("", newFields, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the dataset.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", RequestTier.Short, cancellationToken);

    /// <summary>
    /// Lists items from the dataset, each decoded to a <see cref="JsonNode"/> (objects become
    /// <see cref="JsonObject"/>).
    /// </summary>
    /// <remarks>
    /// The dataset items endpoint returns a bare JSON array (not a data envelope) and reports pagination via
    /// <c>X-Apify-Pagination-*</c> headers, surfaced in the returned page.
    /// </remarks>
    /// <param name="options">Optional item filtering/projection and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<PaginationList<JsonNode?>> ListItemsAsync(DatasetListItemsOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DatasetListItemsOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        var (page, _) = await FetchItemsPageAsync(q, options.Desc ?? false, cancellationToken).ConfigureAwait(false);
        return page;
    }

    /// <summary>
    /// Lazily iterates over all items of the dataset across pages, fetching each page on demand. Mirrors
    /// the reference client's auto-paging <c>listItems</c> iterator.
    /// </summary>
    /// <remarks>
    /// The offset for the next page, and whether iteration continues, follow the number of rows the API
    /// scanned (<c>X-Apify-Pagination-Count</c>) rather than the number of items a page returned. The
    /// <see cref="DatasetListItemsOptions.Clean"/>/<see cref="DatasetListItemsOptions.SkipEmpty"/>/
    /// <see cref="DatasetListItemsOptions.SkipHidden"/> filters apply after <c>offset</c>/<c>limit</c>, so a
    /// page can scan up to the requested limit of rows while returning fewer of them, or none at all;
    /// advancing by the returned count (as <see cref="PaginationList{T}.Count"/> intentionally does, since it
    /// is public and reports this page's item count) would re-scan rows on the next page or stop iteration
    /// in front of rows a filter hid. <see cref="DatasetListItemsOptions.Unwind"/> can conversely leave a page
    /// with more items than rows scanned, so the two counts cannot be mixed. The scanned count is read from
    /// the response header and falls back to the returned item count only when the header itself is absent
    /// (the API always sends it; a missing header means something between the client and the API, such as a
    /// proxy, stripped it).
    /// </remarks>
    /// <param name="options">Optional item filtering/projection; <c>Offset</c>/<c>Limit</c> bound where
    /// iteration starts and the total number of items yielded.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public async IAsyncEnumerable<JsonNode?> IterateItemsAsync(
        DatasetListItemsOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new DatasetListItemsOptions();
        var desc = options.Desc ?? false;
        var baseQuery = new QueryParams();
        options.AppendTo(baseQuery);
        var offset = Math.Max(options.Offset ?? 0, 0);
        var limit = options.Limit;
        var yielded = 0;
        while (true)
        {
            var q = baseQuery.Copy().Set("offset", offset);
            if (limit is not null)
            {
                q.Set("limit", Math.Max(limit.Value - yielded, 0));
            }

            var (page, scannedCount) = await FetchItemsPageAsync(q, desc, cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items)
            {
                yield return item;
                yielded++;
            }

            offset += (int)scannedCount;
            if (scannedCount == 0 || offset >= page.Total || (limit is not null && yielded >= limit.Value))
            {
                yield break;
            }
        }
    }

    /// <summary>
    /// Fetches a single page of dataset items. The endpoint returns a bare JSON array with pagination in
    /// <c>X-Apify-Pagination-*</c> response headers. Returns the page alongside the number of rows the API
    /// scanned to produce it (<c>X-Apify-Pagination-Count</c>), which the offset iterator paginates by (see
    /// <see cref="IterateItemsAsync"/>); it falls back to the item count when the header is absent.
    /// </summary>
    private async Task<(PaginationList<JsonNode?> Page, long ScannedCount)> FetchItemsPageAsync(QueryParams q, bool desc, CancellationToken cancellationToken)
    {
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("items"));
        using var response = await _http.CallAsync(HttpMethod.Get, url, timeout: _ctx.RequestTimeout, tier: RequestTier.Long, cancellationToken: cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var items = new List<JsonNode?>();
        if (Json.Decode(body) is JsonArray array)
        {
            foreach (var item in array)
            {
                items.Add(item);
            }
        }

        var count = items.Count;
        var page = PaginationList<JsonNode?>.FromItems(
            items,
            HeaderInt(response, "X-Apify-Pagination-Total", count),
            HeaderInt(response, "X-Apify-Pagination-Offset", 0),
            HeaderInt(response, "X-Apify-Pagination-Limit", count),
            desc);
        var scannedCount = HeaderInt(response, "X-Apify-Pagination-Count", count);
        return (page, scannedCount);
    }

    /// <summary>
    /// Downloads dataset items serialized in the given format, returning the raw bytes. Unlike
    /// <see cref="ListItemsAsync"/> (parsed items), this returns the items already serialized to JSON, CSV,
    /// XLSX, XML, RSS or HTML — useful for exporting. Bytes (not a decoded string) are returned so binary
    /// formats such as <see cref="DownloadItemsFormat.Xlsx"/> (a ZIP-based export) are not corrupted; decode
    /// text formats yourself, e.g. <c>System.Text.Encoding.UTF8.GetString(bytes)</c>.
    /// </summary>
    /// <param name="format">The output format.</param>
    /// <param name="options">Optional format-specific and filtering options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<byte[]> DownloadItemsAsync(DownloadItemsFormat format, DatasetDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddString("format", format.ToWireValue());
        (options ?? new DatasetDownloadOptions()).AppendTo(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("items"));
        using var response = await _http.CallAsync(HttpMethod.Get, url, timeout: _ctx.RequestTimeout, tier: RequestTier.Long, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pushes one or more items to the dataset.</summary>
    /// <param name="items">Must serialize to a JSON object or an array of objects.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task PushItemsAsync(object items, CancellationToken cancellationToken = default)
    {
        var url = _ctx.MergedParams(new QueryParams()).ApplyToUrl(_ctx.SubUrl("items"));
        using var response = await _http.CallAsync(
            HttpMethod.Post,
            url,
            Json.Encode(items),
            ResourceContext.ContentTypeJsonCharset,
            timeout: _ctx.RequestTimeout,
            tier: RequestTier.Medium,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns statistical information about the dataset.</summary>
    /// <remarks>
    /// Throws if the dataset does not exist (no longer resolves to <c>null</c>): the statistics endpoint has
    /// no meaning apart from its parent dataset, so a missing dataset is reported as an error rather than an
    /// ambiguous empty result, matching the reference client.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonObject> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var body = await _ctx.GetRawRequiredAsync("statistics", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return Json.DecodeData(body) as JsonObject ?? new JsonObject();
    }

    /// <summary>
    /// Builds a public URL for downloading this dataset's items.
    /// </summary>
    /// <remarks>
    /// It fetches the dataset, and if the dataset exposes a URL-signing secret key (i.e. it is private),
    /// appends an HMAC-SHA256 signature so the URL grants access without an API token.
    /// <paramref name="expiresInSecs"/> optionally bounds the validity of a signed URL (<c>null</c> for
    /// non-expiring). The URL is built from the configured public base URL.
    /// </remarks>
    /// <param name="options">Optional item filtering/projection options forwarded into the URL.</param>
    /// <param name="expiresInSecs">Optional expiry in seconds for a signed URL.</param>
    /// <param name="format">Output format served by the URL (<c>null</c> defaults to <see cref="DownloadItemsFormat.Json"/>).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<string> CreateItemsPublicUrlAsync(
        DatasetListItemsOptions? options = null,
        int? expiresInSecs = null,
        DownloadItemsFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new DatasetListItemsOptions()).AppendTo(q);
        if (format is not null)
        {
            q.AddString("format", format.Value.ToWireValue());
        }

        var dataset = await GetAsync(cancellationToken).ConfigureAwait(false);
        if (dataset is not null)
        {
            var secret = JsonValues.String(dataset.ToJsonObject(), "urlSigningSecretKey");
            if (secret is not null)
            {
                var signature = Signatures.SignStorageContent(secret, dataset.Id ?? string.Empty, expiresInSecs);
                q.AddString("signature", signature);
            }
        }

        return q.ApplyToUrl(_ctx.PublicUrl("items"));
    }

    private static long HeaderInt(HttpResponseMessage response, string name, long fallback)
    {
        if (response.Headers.TryGetValues(name, out var values)
            || response.Content.Headers.TryGetValues(name, out values))
        {
            foreach (var value in values)
            {
                if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
        }

        return fallback;
    }

    /// <summary>
    /// Returns this client with every subsequent call's timeout set to <paramref name="timeout"/>,
    /// overriding the tier default (see the "Timeout tiers" section of the top-level README). Pass
    /// <see cref="TimeSpan.Zero"/> for no timeout, matching the reference client's <c>'noTimeout'</c>. A
    /// value above <see cref="ApifyClientOptions.TimeoutSecs"/> (the overall budget) is capped at it —
    /// raise <see cref="ApifyClientOptions.TimeoutSecs"/> itself to allow a longer per-call timeout.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public DatasetClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
