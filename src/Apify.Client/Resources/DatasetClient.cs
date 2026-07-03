using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
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

    internal static DatasetClient Nested(HttpClientCore http, string baseUrl, string subPath)
        => new(http, ResourceContext.Collection(http, baseUrl, subPath));

    internal DatasetClient WithPublicBase(string publicBaseUrl)
    {
        _ctx.WithPublicOrigin(publicBaseUrl);
        return this;
    }

    /// <summary>Fetches the dataset metadata, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Dataset?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new Dataset(obj) : null;
    }

    /// <summary>Updates the dataset metadata (e.g. name, title) and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Dataset> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new Dataset(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the dataset.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

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
        var url = q.ApplyToUrl(_ctx.SubUrl("items"));
        using var response = await _http.CallAsync(HttpMethod.Get, url, timeout: _ctx.RequestTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
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
        return PaginationList<JsonNode?>.FromItems(
            items,
            HeaderInt(response, "X-Apify-Pagination-Total", count),
            HeaderInt(response, "X-Apify-Pagination-Offset", 0),
            HeaderInt(response, "X-Apify-Pagination-Limit", count),
            options.Desc ?? false);
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
        var url = q.ApplyToUrl(_ctx.SubUrl("items"));
        using var response = await _http.CallAsync(HttpMethod.Get, url, timeout: _ctx.RequestTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Pushes one or more items to the dataset.</summary>
    /// <param name="items">Must serialize to a JSON object or an array of objects.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task PushItemsAsync(object items, CancellationToken cancellationToken = default)
    {
        using var response = await _http.CallAsync(
            HttpMethod.Post,
            _ctx.SubUrl("items"),
            Json.Encode(items),
            ResourceContext.ContentTypeJsonCharset,
            timeout: _ctx.RequestTimeout,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Returns statistical information about the dataset, or <c>null</c> if unavailable.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonObject?> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var body = await _ctx.GetRawAsync("statistics", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return body is null ? null : Json.DecodeData(body) as JsonObject;
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
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<string> CreateItemsPublicUrlAsync(
        DatasetListItemsOptions? options = null,
        int? expiresInSecs = null,
        CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new DatasetListItemsOptions()).AppendTo(q);
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
}
