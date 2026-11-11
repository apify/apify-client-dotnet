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
    /// The query parameters, converted as for API calls: <c>null</c> values are left out, booleans become <c>1</c>/<c>0</c>,
    /// dates ISO 8601 in UTC (a <see cref="DateTime"/> of unspecified kind is taken as UTC), lists their comma-joined values
    /// and other values their invariant text. A query in the path is kept and these are appended.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Query { get; init; }

    /// <summary>
    /// Additional request headers. They reach the Actor's server as they are, <c>Authorization</c> included, and
    /// <c>X-Apify-Authorization</c> replaces the token. <c>Content-Type</c> equals <see cref="ContentType"/>. A
    /// <c>Content-Encoding</c> marks a body that is already encoded and is sent without compressing it (<c>identity</c> opts
    /// out of compression). Other <c>Content-*</c> headers, such as <c>Content-Disposition</c>, are sent only with a
    /// <see cref="Body"/>. <c>Content-Length</c> is ignored, since the client sets it.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Headers { get; init; }

    /// <summary>
    /// The request body: <c>byte[]</c> and <c>string</c> are sent as they are, any other value is serialized to JSON, and
    /// <c>null</c> sends no body.
    /// </summary>
    public object? Body { get; init; }

    /// <summary>
    /// The content type of the body. Defaults to <c>application/json</c> for a serialized value,
    /// <c>text/plain; charset=utf-8</c> for a <c>string</c> and <c>application/octet-stream</c> for bytes.
    /// </summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The <c>X-Actor-Request-ID</c> header of the request a multi-tenant Server Actor is handling, sent on as the
    /// same header so the called Actor charges at that request's pricing tier. It replaces an
    /// <c>X-Actor-Request-ID</c> in <see cref="Headers"/>. Use a client created with the run's <c>APIFY_TOKEN</c>.
    /// </summary>
    public string? ParentRequestId { get; init; }

    /// <summary>
    /// The compressor of the request body, for example <see cref="GzipHttpCompressor"/>. Without it the body is sent
    /// uncompressed, since the Actor's server may not decode a compressed body. Set it only if the server decodes the
    /// resulting <c>Content-Encoding</c>. Matches the reference client's <c>compression</c> option.
    /// </summary>
    public HttpCompressor? Compressor { get; init; }

    /// <summary>
    /// Whether to return the response body unread, to read it with <see cref="Resources.ActorServerResponse.ReadAsStreamAsync"/>.
    /// The response holds its connection until disposed. Defaults to <c>false</c>, which reads the whole body first.
    /// </summary>
    public bool StreamResponse { get; init; }

    /// <summary>
    /// The timeout of the HTTP request. Defaults to the <c>Long</c> tier, which covers the cold start of an idle Actor. With
    /// <see cref="StreamResponse"/> it bounds the wait for the server to start responding. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    public TimeSpan? Timeout { get; init; }
}
