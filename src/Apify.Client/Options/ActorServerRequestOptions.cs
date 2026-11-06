using System;
using System.Collections.Generic;
using System.Net.Http;
using Apify.Client.Http;

namespace Apify.Client.Options;

/// <summary>Configures a request to an Actor's server. All fields are optional.</summary>
public sealed class ActorServerRequestOptions
{
    /// <summary>The HTTP method of the request. Defaults to <c>GET</c>.</summary>
    public HttpMethod? Method { get; init; }

    /// <summary>
    /// The query parameters of the request, converted as for API calls: <c>null</c> values are left out, booleans
    /// become <c>1</c>/<c>0</c> and other values use their invariant text form. A query in the path is kept and
    /// these are appended to it.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Query { get; init; }

    /// <summary>
    /// Additional request headers. They reach the Actor's server as they are, <c>Authorization</c> included, and
    /// a header of the token's name (<c>X-Apify-Authorization</c>) replaces the token. A <c>Content-Type</c> header
    /// is the same as <see cref="ContentType"/>. A <c>Content-Encoding</c> header marks a body that is already
    /// encoded, which is then sent without compressing it (<c>identity</c> opts out of compression).
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// The request body, serialized as for API calls: raw bytes (<c>byte[]</c>) and a <c>string</c> are sent as
    /// they are, and any other value is serialized to JSON. <c>null</c> sends no body.
    /// </summary>
    public object? Body { get; init; }

    /// <summary>
    /// The content type of the body. Defaults to <c>application/json</c> for a serialized value,
    /// <c>text/plain; charset=utf-8</c> for a <c>string</c> and <c>application/octet-stream</c> for bytes.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The compressor for the request body, for example <see cref="GzipHttpCompressor"/>. The body is sent
    /// uncompressed when it is not set, since the Actor's server may not decode a compressed body. The body is
    /// compressed under the same conditions as for API calls. Set it only if the Actor's server decodes the
    /// resulting <c>Content-Encoding</c>. Matches the reference client's <c>compression</c> option.
    /// </summary>
    public HttpCompressor? Compressor { get; init; }

    /// <summary>
    /// Whether to return the response body unread, to read it as a stream with
    /// <see cref="Resources.ActorServerResponse.ReadAsStreamAsync"/>. The response then holds its connection open
    /// until it is disposed. Defaults to <c>false</c>, which reads the whole body before returning.
    /// </summary>
    public bool StreamResponse { get; init; }

    /// <summary>
    /// The timeout of the HTTP request. Defaults to the client's <c>Long</c> timeout tier, which covers the cold
    /// start of an idle Actor. With <see cref="StreamResponse"/>, it bounds the wait for the server to start
    /// responding. A value above <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
