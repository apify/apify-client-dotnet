using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Apify.Client.Internal;

/// <summary>
/// Validates a successful JSON response against the schema the Apify OpenAPI specification declares for
/// its operation, the way the reference client's <c>parseResponse</c> does with its generated schemas.
/// </summary>
/// <remarks>
/// The schemas come from the embedded <c>ResponseSchemas.json</c>, generated from the specification by
/// <c>tools/generate_response_schemas.py</c>. Validation covers JSON types, required properties (including
/// nullability), nested properties and array items, and the <c>allOf</c>/<c>anyOf</c>/<c>oneOf</c>
/// combinators. Like the reference client's schemas, it lets unknown properties and unknown enum values
/// through, so an API that grows new fields or values does not break existing clients. A request that
/// matches no documented operation is not validated.
/// </remarks>
internal static class ResponseSchemas
{
    private const string ResourceName = "Apify.Client.ResponseSchemas.json";
    private const string ApiVersionSegment = "/v2/";
    private const string RefPrefix = "#/components/schemas/";

    /// <summary>The most issues reported for one response, so a badly wrong body stays readable.</summary>
    private const int MaxReportedIssues = 10;

    private static readonly Lazy<SchemaSet> Loaded = new(Load);

    /// <summary>The OpenAPI specification version the embedded schemas were generated from.</summary>
    public static string SpecVersion => Loaded.Value.SpecVersion;

    /// <summary>
    /// Validates <paramref name="body"/> against the schema documented for the operation identified by
    /// <paramref name="method"/> and <paramref name="path"/> (the request path, optionally with a query).
    /// Returns the problems found, empty when the body is valid or the operation is not documented.
    /// </summary>
    public static IReadOnlyList<string> Validate(string method, string path, JsonNode? body)
    {
        var set = Loaded.Value;
        var operation = set.Find(method, path);
        if (operation is null)
        {
            return Array.Empty<string>();
        }

        var issues = new List<string>();
        set.Check(body, operation, "$", issues);
        return issues;
    }

    private static SchemaSet Load()
    {
        using var stream = typeof(ResponseSchemas).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The embedded resource " + ResourceName + " is missing.");
        var document = (JsonObject)JsonNode.Parse(stream)!;
        return new SchemaSet(
            (string)document["specVersion"]!,
            document["operations"]!.AsObject(),
            document["schemas"]!.AsObject());
    }

    private sealed class SchemaSet
    {
        private readonly JsonObject _schemas;
        private readonly Dictionary<string, List<OperationSchema>> _operationsByMethod = new(StringComparer.Ordinal);

        public SchemaSet(string specVersion, JsonObject operations, JsonObject schemas)
        {
            SpecVersion = specVersion;
            _schemas = schemas;
            foreach (var (key, schema) in operations)
            {
                var space = key.IndexOf(' ', StringComparison.Ordinal);
                var method = key[..space];
                var template = key[(space + 1)..];
                if (!_operationsByMethod.TryGetValue(method, out var list))
                {
                    list = new List<OperationSchema>();
                    _operationsByMethod[method] = list;
                }

                list.Add(new OperationSchema(template, schema!));
            }

            // The most specific template wins (e.g. `/runs/last` over `/runs/{runId}`): fewest parameters first.
            foreach (var list in _operationsByMethod.Values)
            {
                list.Sort(static (a, b) => a.ParameterCount.CompareTo(b.ParameterCount));
            }
        }

        public string SpecVersion { get; }

        public JsonNode? Find(string method, string path)
        {
            if (!_operationsByMethod.TryGetValue(method.ToUpperInvariant(), out var candidates))
            {
                return null;
            }

            var normalized = NormalizePath(path);
            foreach (var candidate in candidates)
            {
                if (candidate.Pattern.IsMatch(normalized))
                {
                    return candidate.Schema;
                }
            }

            return null;
        }

        /// <summary>Appends a message to <paramref name="issues"/> for each way <paramref name="value"/> violates <paramref name="schema"/>.</summary>
        public void Check(JsonNode? value, JsonNode? schema, string location, List<string> issues)
        {
            if (schema is not JsonObject rules || issues.Count >= MaxReportedIssues)
            {
                return;
            }

            if (rules["$ref"] is JsonNode reference)
            {
                Check(value, Resolve((string)reference!), location, issues);
            }

            if (rules["allOf"] is JsonArray all)
            {
                foreach (var part in all)
                {
                    Check(value, part, location, issues);
                }
            }

            if (rules["anyOf"] is JsonArray any)
            {
                CheckAny(value, any, location, issues);
            }

            if (rules["oneOf"] is JsonArray one)
            {
                // A value satisfying several alternatives is accepted too, like the reference client's unions.
                CheckAny(value, one, location, issues);
            }

            if (rules["type"] is JsonNode typeNode && !MatchesType(value, typeNode))
            {
                issues.Add(location + ": expected " + DescribeType(typeNode) + " but got " + DescribeKind(value));
                return;
            }

            if (value is JsonObject obj)
            {
                CheckObject(obj, rules, location, issues);
            }
            else if (value is JsonArray array && rules["items"] is JsonNode items)
            {
                for (var i = 0; i < array.Count; i++)
                {
                    Check(array[i], items, location + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", issues);
                }
            }
        }

        private void CheckObject(JsonObject obj, JsonObject rules, string location, List<string> issues)
        {
            if (rules["required"] is JsonArray required)
            {
                foreach (var name in required)
                {
                    if (!obj.ContainsKey((string)name!))
                    {
                        issues.Add(location + "." + (string)name! + ": required property is missing");
                    }
                }
            }

            if (rules["properties"] is JsonObject properties)
            {
                foreach (var (name, propertySchema) in properties)
                {
                    // An absent optional property is fine; an absent required one was reported above.
                    if (obj.TryGetPropertyValue(name, out var propertyValue))
                    {
                        Check(propertyValue, propertySchema, location + "." + name, issues);
                    }
                }
            }
        }

        private void CheckAny(JsonNode? value, JsonArray alternatives, string location, List<string> issues)
        {
            foreach (var alternative in alternatives)
            {
                var attempt = new List<string>();
                Check(value, alternative, location, attempt);
                if (attempt.Count == 0)
                {
                    return;
                }
            }

            issues.Add(location + ": matches none of the allowed variants (got " + DescribeKind(value) + ")");
        }

        private JsonNode? Resolve(string reference)
        {
            if (!reference.StartsWith(RefPrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unsupported schema reference: " + reference);
            }

            return _schemas[reference[RefPrefix.Length..]];
        }

        private static bool MatchesType(JsonNode? value, JsonNode typeNode)
            => typeNode is JsonArray types
                ? types.Any(t => MatchesSingleType(value, (string)t!))
                : MatchesSingleType(value, (string)typeNode!);

        private static bool MatchesSingleType(JsonNode? value, string type)
        {
            if (value is null)
            {
                return type == "null";
            }

            return type switch
            {
                "object" => value is JsonObject,
                "array" => value is JsonArray,
                "string" => value.GetValueKind() == JsonValueKind.String,
                "boolean" => value.GetValueKind() is JsonValueKind.True or JsonValueKind.False,
                "number" => value.GetValueKind() == JsonValueKind.Number,
                "integer" => IsInteger(value),
                _ => false,
            };
        }

        private static bool IsInteger(JsonNode value)
        {
            if (value.GetValueKind() != JsonValueKind.Number)
            {
                return false;
            }

            var number = value.GetValue<JsonElement>();
            return number.TryGetInt64(out _) || (number.TryGetDouble(out var d) && d == Math.Floor(d) && !double.IsInfinity(d));
        }

        private static string DescribeType(JsonNode typeNode)
            => typeNode is JsonArray types ? string.Join(" or ", types.Select(static t => (string)t!)) : (string)typeNode!;

        private static string DescribeKind(JsonNode? value) => value is null ? "null" : value.GetValueKind().ToString().ToLowerInvariant();

        private static string NormalizePath(string path)
        {
            var query = path.IndexOf('?', StringComparison.Ordinal);
            var withoutQuery = query >= 0 ? path[..query] : path;
            var version = withoutQuery.IndexOf(ApiVersionSegment, StringComparison.Ordinal);
            return (version >= 0 ? withoutQuery[version..] : withoutQuery).TrimEnd('/');
        }
    }

    private sealed class OperationSchema
    {
        /// <summary>Matches a path parameter (<c>{name}</c>) in a template after <see cref="Regex.Escape(string)"/> escaped its opening brace.</summary>
        private static readonly Regex Parameter = new(@"\\\{[^}/]+\}", RegexOptions.Compiled);

        public OperationSchema(string template, JsonNode schema)
        {
            Schema = schema;
            ParameterCount = template.Count(static c => c == '{');
            Pattern = new Regex("^" + Parameter.Replace(Regex.Escape(template), "[^/]+") + "$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        }

        public JsonNode Schema { get; }

        public int ParameterCount { get; }

        public Regex Pattern { get; }
    }
}
