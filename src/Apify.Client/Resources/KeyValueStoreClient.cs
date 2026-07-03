using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for a specific key-value store (and run-nested variants).</summary>
public sealed class KeyValueStoreClient
{
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;

    private KeyValueStoreClient(HttpClientCore http, ResourceContext ctx)
    {
        _http = http;
        _ctx = ctx;
    }

    internal static KeyValueStoreClient ForId(HttpClientCore http, string baseUrl, string id)
        => new(http, ResourceContext.Single(http, baseUrl, "key-value-stores", id));

    internal static KeyValueStoreClient Nested(HttpClientCore http, string baseUrl, string subPath)
        => new(http, ResourceContext.Collection(http, baseUrl, subPath));

    internal KeyValueStoreClient WithPublicBase(string publicBaseUrl)
    {
        _ctx.WithPublicOrigin(publicBaseUrl);
        return this;
    }

    /// <summary>Fetches the store metadata, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<KeyValueStore?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is System.Text.Json.Nodes.JsonObject obj ? new KeyValueStore(obj) : null;
    }

    /// <summary>Updates the store metadata (e.g. name) and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<KeyValueStore> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new KeyValueStore(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the store.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>Lists the keys stored in this key-value store.</summary>
    /// <param name="options">Optional key-listing filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<KeyValueStoreKeysPage> ListKeysAsync(ListKeysOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListKeysOptions()).AppendTo(q);
        return KeyValueStoreKeysPage.FromData(await _ctx.GetResourceRequiredAsync("keys", q, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Reports whether a record with the given key exists.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<bool> RecordExistsAsync(string key, CancellationToken cancellationToken = default)
        => _ctx.HeadExistsAsync("records/" + ResourceContext.EncodePathSegment(key), new QueryParams(), cancellationToken);

    /// <summary>
    /// Fetches a record by key, or <c>null</c> if it does not exist. Like the reference client, it requests
    /// the record as an attachment so the API returns the raw bytes directly.
    /// </summary>
    /// <param name="key">The record key.</param>
    /// <param name="options">Optional fetch options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<KeyValueStoreRecord?> GetRecordAsync(string key, GetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new GetRecordOptions { Attachment = true };
        var q = new QueryParams();
        options.AppendTo(q);
        var url = _ctx.MergedParams(q).ApplyToUrl(_ctx.SubUrl("records/" + ResourceContext.EncodePathSegment(key)));
        try
        {
            using var response = await _http.CallAsync(HttpMethod.Get, url, timeout: _ctx.RequestTimeout, cancellationToken: cancellationToken).ConfigureAwait(false);
            // Read the raw bytes (not a decoded string) so binary records survive the round-trip intact.
            var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var contentType = response.Content.Headers.ContentType?.ToString();
            return new KeyValueStoreRecord(key, body, string.IsNullOrEmpty(contentType) ? null : contentType);
        }
        catch (ApifyApiException e) when (HttpClientCore.IsNotFound(e))
        {
            return null;
        }
    }

    /// <summary>
    /// Stores a record with raw bytes and the given content type, honoring the given write options
    /// (<c>TimeoutSecs</c>, <c>DoNotRetryTimeouts</c>).
    /// </summary>
    /// <param name="key">The record key.</param>
    /// <param name="value">The raw record bytes.</param>
    /// <param name="contentType">The record's MIME type.</param>
    /// <param name="options">Optional write options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task SetRecordAsync(string key, byte[] value, string contentType, SetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SetRecordOptions();
        var timeout = options.TimeoutSecs is not null ? TimeSpan.FromSeconds(options.TimeoutSecs.Value) : (TimeSpan?)null;
        return _ctx.PutRawAsync(
            "records/" + ResourceContext.EncodePathSegment(key),
            new QueryParams(),
            value,
            contentType,
            timeout,
            options.DoNotRetryTimeouts,
            cancellationToken);
    }

    /// <summary>Stores a record holding the JSON serialization of <paramref name="value"/>.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="value">Any JSON-serializable value.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task SetRecordJsonAsync(string key, object? value, CancellationToken cancellationToken = default)
        => SetRecordAsync(key, System.Text.Encoding.UTF8.GetBytes(Json.Encode(value)), ResourceContext.ContentTypeJsonCharset, null, cancellationToken);

    /// <summary>Deletes a record by key.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteRecordAsync(string key, CancellationToken cancellationToken = default)
        => _ctx.DeleteResourceAsync("records/" + ResourceContext.EncodePathSegment(key), cancellationToken);

    /// <summary>
    /// Builds a public URL for fetching the given record. It fetches the store, and if the store exposes a
    /// URL-signing secret key (i.e. it is private), appends an HMAC-SHA256 signature so the URL grants
    /// access without an API token. The URL is built from the configured public base URL.
    /// </summary>
    /// <param name="key">The record key.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<string> GetRecordPublicUrlAsync(string key, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        var store = await GetAsync(cancellationToken).ConfigureAwait(false);
        if (store is not null)
        {
            var secret = JsonValues.String(store.ToJsonObject(), "urlSigningSecretKey");
            if (secret is not null)
            {
                q.AddString("signature", Signatures.CreateHmacSignature(secret, key));
            }
        }

        return q.ApplyToUrl(_ctx.PublicUrl("records/" + ResourceContext.EncodePathSegment(key)));
    }

    /// <summary>
    /// Builds a public URL for listing this store's keys, forwarding the given key-listing filters into the
    /// URL. As with <see cref="GetRecordPublicUrlAsync"/>, a signature is appended for private stores unless
    /// the caller already supplied one. <paramref name="expiresInSecs"/> optionally bounds a signed URL.
    /// </summary>
    /// <param name="options">Optional key-listing filters forwarded into the URL.</param>
    /// <param name="expiresInSecs">Optional expiry in seconds for a signed URL.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<string> CreateKeysPublicUrlAsync(ListKeysOptions? options = null, int? expiresInSecs = null, CancellationToken cancellationToken = default)
    {
        options ??= new ListKeysOptions();
        var q = new QueryParams();
        options.AppendTo(q);
        if (options.Signature is null)
        {
            var store = await GetAsync(cancellationToken).ConfigureAwait(false);
            if (store is not null)
            {
                var secret = JsonValues.String(store.ToJsonObject(), "urlSigningSecretKey");
                if (secret is not null)
                {
                    q.AddString("signature", Signatures.SignStorageContent(secret, store.Id ?? string.Empty, expiresInSecs));
                }
            }
        }

        return q.ApplyToUrl(_ctx.PublicUrl("keys"));
    }
}
