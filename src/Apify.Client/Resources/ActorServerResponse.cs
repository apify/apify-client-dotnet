using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Internal;

namespace Apify.Client.Resources;

/// <summary>
/// The response of an Actor's server, whatever its status. Dispose it when done; a response read as a stream
/// keeps its connection open until then.
/// </summary>
public sealed class ActorServerResponse : IDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly Lazy<IReadOnlyDictionary<string, string>> _headers;

    internal ActorServerResponse(HttpResponseMessage response)
    {
        _response = response;
        _headers = new Lazy<IReadOnlyDictionary<string, string>>(() => CollectHeaders(response));
    }

    /// <summary>The HTTP status code of the response.</summary>
    public int StatusCode => (int)_response.StatusCode;

    /// <summary>Whether the status code is in the 2xx range.</summary>
    public bool IsSuccess => _response.IsSuccessStatusCode;

    /// <summary>The response headers, names compared case-insensitively; repeated values are joined with <c>, </c>.</summary>
    public IReadOnlyDictionary<string, string> Headers => _headers.Value;

    /// <summary>The <c>Content-Type</c> header of the response, or <c>null</c> if it has none.</summary>
    public string? ContentType => _response.Content.Headers.ContentType?.ToString();

    /// <summary>Reads the response body as raw bytes.</summary>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    public Task<byte[]> ReadAsBytesAsync(CancellationToken cancellationToken = default)
        => _response.Content.ReadAsByteArrayAsync(cancellationToken);

    /// <summary>Reads the response body as text, decoded by the charset of its content type (UTF-8 by default).</summary>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    public Task<string> ReadAsStringAsync(CancellationToken cancellationToken = default)
        => _response.Content.ReadAsStringAsync(cancellationToken);

    /// <summary>
    /// Reads the response body as a stream. With <see cref="Options.ActorServerRequestOptions.StreamResponse"/> the body
    /// arrives as it is received. The stream is valid until this response is disposed.
    /// </summary>
    public Task<Stream> ReadAsStreamAsync(CancellationToken cancellationToken = default)
        => _response.Content.ReadAsStreamAsync(cancellationToken);

    /// <summary>Reads the response body as JSON (<c>null</c> for an empty body).</summary>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <exception cref="InvalidResponseBodyException">The body is not valid JSON.</exception>
    public async Task<JsonNode?> ReadAsJsonAsync(CancellationToken cancellationToken = default)
    {
        var body = await _response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return Json.Decode(body);
        }
        catch (JsonException ex)
        {
            throw new InvalidResponseBodyException(
                "response body is not valid JSON: " + ex.Message,
                StatusCode,
                _response.RequestMessage?.Method.Method ?? string.Empty,
                _response.RequestMessage?.RequestUri is { } uri ? HttpClientCore.ExtractPath(uri.ToString()) : string.Empty,
                body,
                ex);
        }
    }

    /// <summary>Releases the connection of the response.</summary>
    public void Dispose() => _response.Dispose();

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        foreach (var header in response.Content.Headers)
        {
            headers[header.Key] = string.Join(", ", header.Value);
        }

        return headers;
    }
}
