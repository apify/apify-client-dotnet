using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.CompilerServices;
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

    internal static KeyValueStoreClient Nested(HttpClientCore http, string baseUrl, string subPath, QueryParams? inheritedParams = null)
        => new(http, ResourceContext.NestedSingleton(http, baseUrl, subPath, inheritedParams));

    internal KeyValueStoreClient WithPublicBase(string publicBaseUrl)
    {
        _ctx.WithPublicOrigin(publicBaseUrl);
        return this;
    }

    /// <summary>Fetches the store metadata, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<KeyValueStore?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return data is System.Text.Json.Nodes.JsonObject obj ? new KeyValueStore(obj) : null;
    }

    /// <summary>Updates the store metadata (e.g. name) and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<KeyValueStore> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new KeyValueStore(await _ctx.UpdateResourceAsync("", newFields, RequestTier.Long, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the store.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", RequestTier.Short, cancellationToken);

    /// <summary>Lists the keys stored in this key-value store.</summary>
    /// <param name="options">Optional key-listing filters and pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<KeyValueStoreKeysPage> ListKeysAsync(ListKeysOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListKeysOptions()).AppendTo(q);
        return KeyValueStoreKeysPage.FromData(await _ctx.GetResourceRequiredAsync("keys", q, RequestTier.Medium, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lazily iterates over the store's keys across pages, fetching each page on demand. Mirrors the
    /// reference client's chunked <c>listKeys()</c> iteration.
    /// </summary>
    /// <remarks>
    /// With no options it fetches pages of up to <see cref="IterateKeysOptions.DefaultMaxPageLimit"/> keys
    /// until the store is exhausted. <c>Limit</c> caps the total number of keys yielded across all pages —
    /// <c>0</c> yields none, without issuing any request (the API itself reads a literal <c>limit=0</c> as
    /// unbounded, not zero, so this is checked before building the request rather than left to the API), and
    /// must not be negative. <c>MaxPageLimit</c> caps the page size (never sent to the API itself) and must
    /// be positive, and <c>ExclusiveStartKey</c> chooses the starting point (first page only).
    /// </remarks>
    /// <param name="options">Optional iteration options.</param>
    /// <param name="cancellationToken">A token to cancel the iteration.</param>
    /// <exception cref="ArgumentException">
    /// <see cref="IterateKeysOptions.MaxPageLimit"/> is zero or negative, or <see cref="IterateKeysOptions.Limit"/>
    /// is negative.
    /// </exception>
    public async IAsyncEnumerable<KeyValueStoreKey> IterateKeysAsync(
        IterateKeysOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new IterateKeysOptions();
        if (options.MaxPageLimit is <= 0)
        {
            throw new ArgumentException("IterateKeysOptions.MaxPageLimit must be positive.", nameof(options));
        }

        if (options.Limit is < 0)
        {
            throw new ArgumentException("IterateKeysOptions.Limit must not be negative.", nameof(options));
        }

        var maxPageLimit = options.MaxPageLimit ?? IterateKeysOptions.DefaultMaxPageLimit;
        var limit = options.Limit; // total across all pages; null = unbounded
        var nextExclusiveStartKey = options.ExclusiveStartKey;
        var iterated = 0;

        while (true)
        {
            // Checked before building the request, not only after: the API reads a literal `limit=0` as
            // unset (unbounded) rather than "zero", so a Limit of 0 (or one already exhausted by a prior
            // page) must stop here instead of reaching the wire as 0 and fetching a full page regardless.
            if (limit is not null && iterated >= limit.Value)
            {
                yield break;
            }

            var pageLimit = limit is not null ? Math.Min(maxPageLimit, limit.Value - iterated) : maxPageLimit;

            var page = await ListKeysAsync(
                new ListKeysOptions
                {
                    Limit = pageLimit,
                    ExclusiveStartKey = nextExclusiveStartKey,
                    Collection = options.Collection,
                    Prefix = options.Prefix,
                },
                cancellationToken).ConfigureAwait(false);

            if (page.Items.Count == 0)
            {
                yield break;
            }

            foreach (var item in page.Items)
            {
                yield return item;
            }

            iterated += page.Items.Count;

            // Continue only while there is a next cursor to follow — mirrors the reference client
            // (apify-client-js `key_value_store.ts` listKeys iteration), which continues on
            // `nextExclusiveStartKey != null` rather than on `isTruncated` alone. The API answers with
            // `null` on the last page and omits the field entirely for a listing that was not truncated,
            // but `isTruncated` alone is not a safe continuation signal: if it were ever `true` alongside a
            // null/absent cursor, looping on it would re-fetch the same first page forever (no
            // `exclusiveStartKey` reaches the wire for a null cursor), yielding duplicate keys.
            if (page.NextExclusiveStartKey is null || (limit is not null && iterated >= limit.Value))
            {
                yield break;
            }

            nextExclusiveStartKey = page.NextExclusiveStartKey;
        }
    }

    /// <summary>Reports whether a record with the given key exists.</summary>
    /// <param name="key">The record key.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<bool> RecordExistsAsync(string key, CancellationToken cancellationToken = default)
        => _ctx.HeadExistsAsync("records/" + ResourceContext.EncodePathSegment(key), new QueryParams(), RequestTier.Long, cancellationToken);

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
            using var response = await _http.CallAsync(HttpMethod.Get, url, timeout: _ctx.RequestTimeout, tier: RequestTier.Long, cancellationToken: cancellationToken).ConfigureAwait(false);
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
    /// (<c>TimeoutSecs</c>, <c>DoNotRetryTimeouts</c>, <c>ContentEncoding</c>).
    /// </summary>
    /// <param name="key">The record key.</param>
    /// <param name="value">
    /// The raw record bytes. With <see cref="SetRecordOptions.ContentEncoding"/> set, these must already be
    /// encoded that way (e.g. already gzipped) — the client sends them as-is and does not compress them
    /// again.
    /// </param>
    /// <param name="contentType">
    /// The record's MIME type. Worth setting precisely for media and archives: the client skips
    /// compressing a body whose content type already carries its own compression (images, audio, video,
    /// archives, office/ZIP formats, web fonts). <c>application/octet-stream</c> — the usual choice for
    /// data of an unknown or mixed binary type — is treated as compressible, since it could hold anything.
    /// This content-type-based skip does not apply when <see cref="SetRecordOptions.ContentEncoding"/> is
    /// set: that always skips the client's own compression, regardless of content type.
    /// </param>
    /// <param name="options">Optional write options.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task SetRecordAsync(string key, byte[] value, string contentType, SetRecordOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new SetRecordOptions();
        var timeout = options.TimeoutSecs is not null ? TimeSpan.FromSeconds(options.TimeoutSecs.Value) : (TimeSpan?)null;
        string? contentEncoding = null;
        if (options.ContentEncoding is not null)
        {
            contentEncoding = options.ContentEncoding.Trim();
            if (contentEncoding.Length == 0)
            {
                throw new ArgumentException("ContentEncoding must not be empty or whitespace-only.", nameof(options));
            }
        }

        return _ctx.PutRawAsync(
            "records/" + ResourceContext.EncodePathSegment(key),
            new QueryParams(),
            value,
            contentType,
            timeout,
            options.DoNotRetryTimeouts,
            RequestTier.Long,
            cancellationToken,
            contentEncoding);
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
        => _ctx.DeleteResourceAsync("records/" + ResourceContext.EncodePathSegment(key), RequestTier.Short, cancellationToken);

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

    /// <summary>
    /// Returns this client with every subsequent call's timeout set to <paramref name="timeout"/>,
    /// overriding the tier default (see the "Timeout tiers" section of the top-level README). Pass
    /// <see cref="TimeSpan.Zero"/> for no timeout, matching the reference client's <c>'noTimeout'</c>. A
    /// value above <see cref="ApifyClientOptions.TimeoutSecs"/> (the overall budget) is capped at it —
    /// raise <see cref="ApifyClientOptions.TimeoutSecs"/> itself to allow a longer per-call timeout.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public KeyValueStoreClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
