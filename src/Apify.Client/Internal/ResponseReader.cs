using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;

namespace Apify.Client.Internal;

/// <summary>
/// Reads a successful JSON response into a <see cref="JsonNode"/> and reports a body that is unparsable
/// (<see cref="InvalidResponseBodyException"/>) or does not match the schema the API specification
/// declares for the operation (<see cref="ResponseValidationException"/>, see <see cref="ResponseSchemas"/>),
/// with the request's method and path as context. Every successful JSON response is decoded through here,
/// matching the reference client's <c>parseResponse</c>.
/// </summary>
internal static class ResponseReader
{
    private const string DataEnvelopeKey = "data";

    /// <summary>Reads a JSON body that is not wrapped in a <c>data</c> envelope (<c>null</c> for an empty body).</summary>
    public static async Task<JsonNode?> ReadJsonAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var node = Parse(response, body);
        ValidateAgainstSpec(response, body, node);
        return node;
    }

    /// <summary>Reads a bare (not enveloped) JSON array body, such as a dataset's items.</summary>
    public static async Task<JsonArray> ReadJsonArrayAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var node = Parse(response, body);
        ValidateAgainstSpec(response, body, node);
        return node as JsonArray ?? throw Invalid(response, body, "the response is not a JSON array");
    }

    /// <summary>Reads a <c>data</c>-enveloped body whose <c>data</c> must be an object.</summary>
    public static async Task<JsonObject> ReadDataObjectAsync(this HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return UnwrapData(response, body) as JsonObject ?? throw Invalid(response, body, "the response's `data` is not an object");
    }

    /// <summary>
    /// Parses an enveloped body, validates it against the specification's schema for the operation, and
    /// returns the unwrapped non-null <c>data</c>.
    /// </summary>
    private static JsonNode UnwrapData(HttpResponseMessage response, string body)
    {
        var node = Parse(response, body);
        ValidateAgainstSpec(response, body, node);
        if (node is not JsonObject envelope || !envelope.TryGetPropertyValue(DataEnvelopeKey, out var data))
        {
            throw Invalid(response, body, "the response is missing the `data` envelope");
        }

        return data ?? throw Invalid(response, body, "the response's `data` is null");
    }

    private static void ValidateAgainstSpec(HttpResponseMessage response, string body, JsonNode? node)
    {
        var issues = ResponseSchemas.Validate(MethodOf(response), PathOf(response), node);
        if (issues.Count > 0)
        {
            throw Invalid(response, body, string.Join("; ", issues));
        }
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
