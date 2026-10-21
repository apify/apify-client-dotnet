using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;

namespace Apify.Client.Internal;

/// <summary>
/// Reads a successful JSON response into a <see cref="JsonNode"/> and reports a body that is unparsable
/// (<see cref="InvalidResponseBodyException"/>) or lacks the shape the API specification promises
/// (<see cref="ResponseValidationException"/>), with the request's method and path as context. Every
/// successful JSON response is decoded through here, matching the reference client's <c>parseResponse</c>.
/// </summary>
internal static class ResponseReader
{
    private const string DataEnvelopeKey = "data";

    /// <summary>Reads a JSON body that is not wrapped in a <c>data</c> envelope (<c>null</c> for an empty body).</summary>
    public static async Task<JsonNode?> ReadJsonAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Parse(response, body);
    }

    /// <summary>
    /// Reads a body wrapped in the <c>{"data": ...}</c> envelope and returns the unwrapped value. The
    /// envelope and a non-null <c>data</c> are required.
    /// </summary>
    public static async Task<JsonNode> ReadDataAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (Parse(response, body) is not JsonObject envelope || !envelope.TryGetPropertyValue(DataEnvelopeKey, out var data))
        {
            throw Invalid(response, body, "the response is missing the `data` envelope");
        }

        return data ?? throw Invalid(response, body, "the response's `data` is null");
    }

    /// <summary>Reads a <c>data</c>-enveloped body whose <c>data</c> must be an object.</summary>
    public static async Task<JsonObject> ReadDataObjectAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (Parse(response, body) is not JsonObject envelope || !envelope.TryGetPropertyValue(DataEnvelopeKey, out var data))
        {
            throw Invalid(response, body, "the response is missing the `data` envelope");
        }

        return data as JsonObject ?? throw Invalid(response, body, "the response's `data` is not an object");
    }

    /// <summary>Reads a bare (not enveloped) JSON array body, such as a dataset's items.</summary>
    public static async Task<JsonArray> ReadJsonArrayAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return Parse(response, body) as JsonArray ?? throw Invalid(response, body, "the response is not a JSON array");
    }

    private static JsonNode? Parse(HttpResponseMessage response, string body)
    {
        try
        {
            return Json.Decode(body);
        }
        catch (JsonException ex)
        {
            throw new InvalidResponseBodyException(
                "response body is not valid JSON: " + ex.Message,
                (int)response.StatusCode,
                MethodOf(response),
                PathOf(response),
                body,
                ex);
        }
    }

    private static ResponseValidationException Invalid(HttpResponseMessage response, string body, string problem)
        => new(
            "response does not match the API specification: " + problem,
            (int)response.StatusCode,
            MethodOf(response),
            PathOf(response),
            body);

    private static string MethodOf(HttpResponseMessage response) => response.RequestMessage?.Method.Method ?? string.Empty;

    private static string PathOf(HttpResponseMessage response)
        => response.RequestMessage?.RequestUri is { } uri ? HttpClientCore.ExtractPath(uri.ToString()) : string.Empty;
}
