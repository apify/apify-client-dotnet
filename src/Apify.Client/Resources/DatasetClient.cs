using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for a specific dataset.</summary>
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

    internal static DatasetClient Nested(HttpClientCore http, string baseUrl, string subPath, QueryParams? inheritedParams = null, TimeSpan? timeout = null)
        => new(http, ResourceContext.NestedSingleton(http, baseUrl, subPath, inheritedParams).WithTimeout(timeout));

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

    /// <summary>Updates the dataset metadata, such as its name or title, and returns the updated dataset.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Dataset> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new Dataset(await _ctx.UpdateResourceAsync("", newFields, RequestTier.Short, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the dataset.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", RequestTier.Short, cancellationToken);

    /// <summary>Lists items from the dataset, each decoded to a <see cref="JsonNode"/>.</summary>
    /// <remarks>
    /// The endpoint returns a bare JSON array and reports pagination in <c>X-Apify-Pagination-*</c> headers, which end up
    /// in the returned page.
    /// </remarks>
    /// <param name="options">Optional item filtering, projection and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<PaginationList<JsonNode?>> ListItemsAsync(DatasetListItemsOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new DatasetListItemsOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        return await FetchItemsPageAsync(q, options.Desc ?? false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lazily iterates all items of the dataset across pages, fetching each page on demand.</summary>
    /// <remarks>
    /// Pagination follows the rows the API scanned (<c>X-Apify-Pagination-Count</c>), not the items a page returned. The
    /// <see cref="DatasetListItemsOptions.Clean"/>, <see cref="DatasetListItemsOptions.SkipEmpty"/> and
    /// <see cref="DatasetListItemsOptions.SkipHidden"/> filters apply after <c>offset</c> and <c>limit</c>, so a page can
    /// scan its full limit and return fewer items, and <see cref="DatasetListItemsOptions.Unwind"/> can return more items
    /// than rows. <see cref="DatasetListItemsOptions.Limit"/> therefore caps the rows scanned (<c>0</c> or <c>null</c> for no
    /// cap). The header lags behind fresh pushes, so the larger of it and the item count is used, capped at the page limit.
    /// </remarks>
    /// <param name="options">
    /// Optional filtering and projection. <c>Offset</c> and <c>Limit</c> bound where iteration starts and the rows scanned,
    /// and <c>ChunkSize</c> sets the page size.
    /// </param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    public IAsyncEnumerable<JsonNode?> IterateItemsAsync(
        DatasetListItemsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new DatasetListItemsOptions();
        var desc = options.Desc ?? false;
        var baseQuery = new QueryParams();
        options.AppendTo(baseQuery);
        return PagedIteration.IterateAsync(
            options.Offset ?? 0,
            options.Limit,
            options.ChunkSize,
            limitCountsRows: true,
            async (offset, pageLimit, token) =>
            {
                var page = await FetchItemsPageAsync(baseQuery.PageQuery(offset, pageLimit), desc, token).ConfigureAwait(false);
                return new FetchedPage<JsonNode?>(page.Items, page.Count);
            },
            cancellationToken);
    }

    /// <summary>Fetches one page of items.</summary>
    internal Task<PaginationList<JsonNode?>> FetchItemsPageAsync(DatasetListItemsOptions options, CancellationToken cancellationToken)
    {
        var q = new QueryParams();
        options.AppendTo(q);
        return FetchItemsPageAsync(q, options.Desc ?? false, cancellationToken);
    }

    /// <summary>
    /// Fetches one page of items. The page's <see cref="PaginationList{T}.Count"/> is the number of rows the API scanned
    /// (<c>X-Apify-Pagination-Count</c>), or the item count when that is larger or the header is missing, since the header
    /// lags fresh pushes.
    /// </summary>
    private async Task<PaginationList<JsonNode?>> FetchItemsPageAsync(QueryParams q, bool desc, CancellationToken cancellationToken)
    {
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("items"));
        using var response = await _http.CallAsync(HttpMethod.Get, url, timeout: _ctx.RequestTimeout, tier: RequestTier.Long, cancellationToken: cancellationToken).ConfigureAwait(false);
        var items = new List<JsonNode?>();
        foreach (var item in await response.ReadJsonArrayAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(item);
        }

        var returned = items.Count;
        return PaginationList<JsonNode?>.FromItems(
            items,
            Math.Max(HeaderInt(response, "X-Apify-Pagination-Count", returned), returned),
            HeaderInt(response, "X-Apify-Pagination-Total", returned),
            HeaderInt(response, "X-Apify-Pagination-Offset", 0),
            HeaderInt(response, "X-Apify-Pagination-Limit", returned),
            desc);
    }

    /// <summary>
    /// Downloads the items serialized in the given format (JSON, CSV, XLSX, XML, RSS or HTML) as raw bytes, which keeps
    /// binary formats such as <see cref="DownloadItemsFormat.Xlsx"/> intact. Decode text formats yourself.
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

    /// <summary>
    /// Downloads the items serialized in the given format as a stream, so a large export need not fit in memory. Takes the
    /// same arguments as <see cref="DownloadItemsAsync"/>.
    /// </summary>
    /// <remarks>
    /// A single attempt, not retried, like <see cref="LogClient.StreamAsync"/>. Dispose the stream to release the connection.
    /// </remarks>
    /// <param name="format">The output format.</param>
    /// <param name="options">Optional format-specific and filtering options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Stream> StreamItemsAsync(DownloadItemsFormat format, DatasetDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        q.AddString("format", format.ToWireValue());
        (options ?? new DatasetDownloadOptions()).AppendTo(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("items"));

        var response = await _http.StreamAsync(url, cancellationToken).ConfigureAwait(false);
        var status = (int)response.StatusCode;
        if (status >= HttpClientCore.MaxSuccessStatus)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            response.Dispose();
            throw HttpClientCore.BuildApiError(status, errorBody, 1, "GET", HttpClientCore.ExtractPath(url));
        }

        return await ResponseOwningStream.CreateAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pushes one or more items to the dataset.</summary>
    /// <remarks>
    /// The API accepts only a JSON object or an array of objects, so any other shape is rejected before a request is sent.
    /// A <see cref="string"/> is sent as already serialized JSON. A push that times out is not retried, because the API
    /// may have stored the items already; an <see cref="Exceptions.ApifyTransportException"/> is thrown instead.
    /// </remarks>
    /// <param name="items">A single item, an array of items, or the JSON serialization of either.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="items"/> serializes to anything but a JSON object or an array of objects.</exception>
    public async Task PushItemsAsync(object items, CancellationToken cancellationToken = default)
    {
        var body = items as string;
        var bodyBytes = body is null ? SerializeItems(items) : null;
        var url = _ctx.MergedParams(new QueryParams()).ApplyToUrl(_ctx.SubUrl("items"));
        using var response = await _http.CallAsync(
            HttpMethod.Post,
            url,
            body,
            ResourceContext.ContentTypeJsonCharset,
            timeout: _ctx.RequestTimeout,
            doNotRetryTimeouts: true,
            bodyBytes: bodyBytes,
            tier: RequestTier.Medium,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Serializes the items to UTF-8 JSON once and checks that they are an object or an array of objects, by reading the
    /// tokens instead of building a document tree.
    /// </summary>
    private static byte[] SerializeItems(object items)
    {
        var bytes = Json.EncodeToUtf8(items);
        if (!IsObjectOrArrayOfObjects(bytes))
        {
            throw new ArgumentException("items must serialize to a JSON object or an array of JSON objects", nameof(items));
        }

        return bytes;
    }

    private static bool IsObjectOrArrayOfObjects(ReadOnlySpan<byte> json)
    {
        var reader = new Utf8JsonReader(json);
        if (!reader.Read())
        {
            return false;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return true;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            return false;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                return false;
            }

            reader.Skip();
        }

        return true;
    }

    /// <summary>Returns statistics about the dataset.</summary>
    /// <remarks>Throws if the dataset does not exist, as in the reference client.</remarks>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonObject> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return await _ctx.GetDataObjectRequiredAsync("statistics", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds a public URL for downloading the dataset's items.</summary>
    /// <remarks>
    /// For a private dataset (one with a URL-signing secret key), the URL carries an HMAC-SHA256 signature, so it works
    /// without an API token.
    /// </remarks>
    /// <param name="options">Optional item filtering and projection, forwarded into the URL.</param>
    /// <param name="expiresInSecs">Expiry of a signed URL in seconds; must be positive. Omit it for a URL that never expires.</param>
    /// <param name="format">The format served by the URL (<c>null</c> for <see cref="DownloadItemsFormat.Json"/>).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<string> CreateItemsPublicUrlAsync(
        DatasetListItemsOptions? options = null,
        int? expiresInSecs = null,
        DownloadItemsFormat? format = null,
        CancellationToken cancellationToken = default)
    {
        Signatures.ValidateExpiry(expiresInSecs, nameof(expiresInSecs));
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
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public DatasetClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
