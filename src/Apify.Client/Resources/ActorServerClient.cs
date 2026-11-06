using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Exceptions;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>
/// A client for the server interface of a specific Actor, obtained with <see cref="ActorClient.Server"/>.
/// </summary>
/// <remarks>
/// A Server Actor (formerly Actor Standby) runs a web server that is called over HTTP without starting a run for
/// each call. This client sends requests to that server, and returns its URL and its OpenAPI specification. See
/// <see href="https://docs.apify.com/platform/actors/running/standby"/>.
/// </remarks>
public sealed class ActorServerClient
{
    /// <summary>The header that carries the API token to an Actor's server. The platform removes it before the request reaches the Actor.</summary>
    private const string ServerAuthorizationHeader = "X-Apify-Authorization";

    /// <summary>The <c>Content-Encoding</c> header, which marks a request body that is already encoded.</summary>
    private const string ContentEncodingHeader = "Content-Encoding";

    /// <summary>The <c>Content-Type</c> header, which describes the request body.</summary>
    private const string ContentTypeHeader = "Content-Type";

    /// <summary>Prefix of the headers that describe the request body; the client sets them on the body, not as plain headers.</summary>
    private const string ContentHeaderPrefix = "Content-";

    /// <summary>The build tag whose build serves the server interface when <c>actorStandby.build</c> is not set.</summary>
    private const string DefaultBuildTag = "latest";

    private const string DefaultRequestPath = "/";

    private const string JsonContentType = "application/json";
    private const string TextContentType = "text/plain; charset=utf-8";
    private const string BinaryContentType = "application/octet-stream";

    /// <summary>The rate limit status the platform answers with when it cannot pass a request on.</summary>
    private const int RateLimitStatus = 429;

    /// <summary>The Service Unavailable status the platform answers with when it cannot pass a request to a server run.</summary>
    private const int ServiceUnavailableStatus = 503;

    /// <summary>
    /// Statuses a server request is retried on. The platform answers with them when it cannot pass a request to a
    /// server run, so a retry usually does not run the Actor's code again. An Actor that returns one itself gets
    /// the request again.
    /// </summary>
    private static readonly int[] ServerRetryStatusCodes = { RateLimitStatus, ServiceUnavailableStatus };

    /// <summary>Matches a build number such as <c>0.1.23</c>: version, minor version and sequence number.</summary>
    private static readonly Regex BuildNumberPattern = new(@"^(\d+)\.(\d+)\.(\d+)$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private readonly HttpClientCore _http;
    private readonly string _baseUrl;
    private readonly ResourceContext _ctx;
    private string? _serverUrl;

    internal ActorServerClient(HttpClientCore http, string baseUrl, string id)
    {
        _http = http;
        _baseUrl = baseUrl;
        Id = id;
        _ctx = ResourceContext.Single(http, baseUrl, "actors", id);
    }

    /// <summary>The Actor's ID (or <c>username~name</c>) as provided.</summary>
    public string Id { get; }

    /// <summary>
    /// Returns the URL of the Actor's server, to call it with another HTTP client, for example one generated from
    /// the server's OpenAPI specification (see <see cref="GetOpenApiSpecAsync"/>).
    /// </summary>
    /// <remarks>
    /// The URL is the Actor's <c>standbyUrl</c>. The first call of this method, <see cref="GetOpenApiSpecAsync"/> or
    /// <see cref="RequestAsync"/> fetches it from the API and this client reuses it afterwards; concurrent first
    /// calls may each fetch it.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="ActorServerException">The Actor is not a Server Actor.</exception>
    /// <exception cref="ApifyApiException">The Actor does not exist.</exception>
    public async Task<string> GetUrlAsync(CancellationToken cancellationToken = default)
        => _serverUrl ?? (await GetServerActorAsync(cancellationToken).ConfigureAwait(false)).StandbyUrl!;

    /// <summary>
    /// Returns the OpenAPI specification of the Actor's server, to generate an HTTP client for it and point that
    /// client at the URL returned by <see cref="GetUrlAsync"/>; <c>null</c> if the Actor's definition does not
    /// include one.
    /// </summary>
    /// <remarks>
    /// The specification is the web server schema of the Actor's definition, taken from the build that serves the
    /// server interface: the build set in <c>actorStandby.build</c> (a tag or a build number), or <c>latest</c> if
    /// none is set. See <see href="https://docs.apify.com/platform/actors/development/actor-definition/web-server-schema"/>.
    /// </remarks>
    /// <param name="cancellationToken">A token to cancel the requests.</param>
    /// <exception cref="ActorServerException">The Actor is not a Server Actor, or the build that serves its server interface does not exist.</exception>
    /// <exception cref="ApifyApiException">The Actor does not exist.</exception>
    public async Task<JsonObject?> GetOpenApiSpecAsync(CancellationToken cancellationToken = default)
    {
        var actor = await GetServerActorAsync(cancellationToken).ConfigureAwait(false);
        var configuredBuild = actor.ActorStandby?["build"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
        var buildRef = string.IsNullOrEmpty(configuredBuild) ? DefaultBuildTag : configuredBuild;

        var buildId = TaggedBuildId(actor, buildRef);
        if (buildId is null && ParseBuildNumber(buildRef) is { } target)
        {
            // Not a tag, so look `buildRef` up as a build number, which the API cannot resolve directly.
            buildId = await FindBuildIdByNumberAsync(buildRef, target, cancellationToken).ConfigureAwait(false);
        }

        var build = buildId is null ? null : await BuildClientFor(buildId).GetAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (build is null)
        {
            throw new ActorServerException($"Build {buildRef} of Actor {Id} does not exist.");
        }

        return build.ActorDefinition?["webServerSchema"] as JsonObject;
    }

    /// <summary>
    /// Sends an HTTP request to the Actor's server and returns its response, whatever its status.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The request goes to the URL returned by <see cref="GetUrlAsync"/> and carries the client's token in the
    /// <c>X-Apify-Authorization</c> header, which the platform consumes, so it never reaches the Actor's code.
    /// Headers in <see cref="ActorServerRequestOptions.Headers"/>, <c>Authorization</c> included, reach the Actor's
    /// server as they are. The first request to an idle Actor waits for the platform to start a server run, which
    /// is why the timeout defaults to the <c>Long</c> tier.
    /// </para>
    /// <para>
    /// A rate limit (429) or Service Unavailable (503) response is retried with the client's retry settings, and
    /// the last one is returned once the retries run out. The Actor's code can return these statuses too, and
    /// then it gets the request again. Other statuses are not retried, since they may come from the Actor's code,
    /// and neither are network errors and timeouts, since the Actor may have already handled the request. A
    /// redirect is returned as it is, since following it would send the token to a host the Actor's code picked.
    /// Request bodies are sent uncompressed unless <see cref="ActorServerRequestOptions.Compressor"/> is set.
    /// </para>
    /// </remarks>
    /// <param name="path">
    /// The path on the Actor's server, for example <c>/search</c>. It is always resolved against the server URL,
    /// so the request, and the token with it, cannot be sent to another host. Pass the query in
    /// <see cref="ActorServerRequestOptions.Query"/>. A fragment (<c>#...</c>) is dropped, since it is never sent
    /// to the server.
    /// </param>
    /// <param name="options">Optional request settings.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <returns>The response of the Actor's server; dispose it when done.</returns>
    /// <exception cref="ActorServerException">The Actor is not a Server Actor.</exception>
    /// <exception cref="ApifyApiException">The Actor does not exist.</exception>
    /// <exception cref="ApifyTransportException">The request failed or timed out without a response.</exception>
    public async Task<ActorServerResponse> RequestAsync(
        string path = DefaultRequestPath,
        ActorServerRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        options ??= new ActorServerRequestOptions();

        var url = BuildRequestUrl(await GetUrlAsync(cancellationToken).ConfigureAwait(false), path, options);
        string? body;
        byte[]? bodyBytes;
        if (options.Body is string rawText)
        {
            body = rawText;
            bodyBytes = null;
        }
        else
        {
            ResourceContext.EncodeInputBody(options.Body, out body, out bodyBytes);
        }

        var policy = new RequestPolicy
        {
            AuthorizationHeader = ServerAuthorizationHeader,
            ThrowOnErrorStatus = false,
            RetryStatusCodes = ServerRetryStatusCodes,
            RetryTransportErrors = false,
            FollowRedirects = false,
            CompressBody = options.Compressor is not null,
            Compressor = options.Compressor,
            StreamResponse = options.StreamResponse,
        };
        var response = await _http.CallAsync(
            options.Method ?? HttpMethod.Get,
            url,
            body,
            options.ContentType ?? FindHeader(options.Headers, ContentTypeHeader) ?? DefaultContentType(options.Body),
            options.Timeout,
            bodyBytes: bodyBytes,
            extraHeaders: WithoutContentHeaders(options.Headers),
            tier: RequestTier.Long,
            explicitContentEncoding: FindHeader(options.Headers, ContentEncodingHeader),
            policy: policy,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return new ActorServerResponse(response);
    }

    /// <summary>
    /// Returns this client with the timeout of the API calls it makes (the Actor and build lookups of
    /// <see cref="GetUrlAsync"/> and <see cref="GetOpenApiSpecAsync"/>) set to <paramref name="timeout"/>,
    /// overriding the tier default (see the "Timeout tiers" section of the top-level README). Pass
    /// <see cref="TimeSpan.Zero"/> for no timeout. A value above <see cref="ApifyClientOptions.TimeoutSecs"/>
    /// is capped at it. It does not affect <see cref="RequestAsync"/>, which takes its timeout from
    /// <see cref="ActorServerRequestOptions.Timeout"/>.
    /// </summary>
    /// <param name="timeout">The timeout to use for the API calls made through this client.</param>
    public ActorServerClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }

    private static string BuildRequestUrl(string serverUrl, string path, ActorServerRequestOptions options)
    {
        var fragment = path.IndexOf('#', StringComparison.Ordinal);
        var serverPath = (fragment >= 0 ? path.Substring(0, fragment) : path).TrimStart('/');
        var query = new QueryParams();
        if (options.Query is not null)
        {
            foreach (var (key, value) in options.Query)
            {
                AddQueryValue(query, key, value);
            }
        }

        return query.ApplyToUrl(serverUrl.TrimEnd('/') + "/" + serverPath);
    }

    private static void AddQueryValue(QueryParams query, string key, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case bool flag:
                query.AddBool(key, flag);
                break;
            case IFormattable formattable:
                query.AddRaw(key, formattable.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                query.AddRaw(key, value.ToString() ?? string.Empty);
                break;
        }
    }

    private static string DefaultContentType(object? body) => body switch
    {
        string => TextContentType,
        byte[] => BinaryContentType,
        _ => JsonContentType,
    };

    /// <summary>The headers that are not about the body (<c>Content-*</c>); those are applied to the body itself.</summary>
    private static Dictionary<string, string>? WithoutContentHeaders(IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        var plain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            if (!header.Key.StartsWith(ContentHeaderPrefix, StringComparison.OrdinalIgnoreCase))
            {
                plain[header.Key] = header.Value;
            }
        }

        return plain;
    }

    private static string? FindHeader(IReadOnlyDictionary<string, string>? headers, string name)
    {
        if (headers is null)
        {
            return null;
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }

    /// <summary>Fetches the Actor, checks that it is a Server Actor, and remembers its server URL.</summary>
    private async Task<Actor> GetServerActorAsync(CancellationToken cancellationToken)
    {
        var actor = new Actor(await _ctx.GetResourceRequiredAsync("", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false));
        if (string.IsNullOrEmpty(actor.StandbyUrl))
        {
            throw new ActorServerException($"Actor {Id} is not a Server Actor.");
        }

        _serverUrl = actor.StandbyUrl;
        return actor;
    }

    private static string? TaggedBuildId(Actor actor, string tag)
        => actor.TaggedBuilds?[tag] is JsonObject tagged && tagged["buildId"] is JsonValue id && id.TryGetValue<string>(out var buildId)
            ? buildId
            : null;

    private async Task<string?> FindBuildIdByNumberAsync(string buildNumber, (int Major, int Minor, int Sequence) target, CancellationToken cancellationToken)
    {
        var builds = new BuildCollectionClient(_http, _ctx.SubUrl(""), "builds");
        if (_ctx.RequestTimeout is { } timeout)
        {
            builds.WithTimeout(timeout);
        }

        // Builds are listed newest first and the sequence numbers of a version grow over time, so meeting an older
        // build of the same version means the target does not exist.
        await foreach (var build in builds.IterateAsync(new ListOptions { Desc = true }, cancellationToken).ConfigureAwait(false))
        {
            if (build.BuildNumber == buildNumber)
            {
                return build.Id;
            }

            if (ParseBuildNumber(build.BuildNumber) is { } parsed
                && parsed.Major == target.Major && parsed.Minor == target.Minor && parsed.Sequence < target.Sequence)
            {
                break;
            }
        }

        return null;
    }

    private BuildClient BuildClientFor(string buildId)
    {
        var client = new BuildClient(_http, _baseUrl, buildId);
        if (_ctx.RequestTimeout is { } timeout)
        {
            client.WithTimeout(timeout);
        }

        return client;
    }

    /// <summary>Splits a build number like <c>0.1.23</c> into its parts, or returns <c>null</c> for anything else.</summary>
    private static (int Major, int Minor, int Sequence)? ParseBuildNumber(string? buildNumber)
    {
        var match = BuildNumberPattern.Match(buildNumber ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        return (
            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
    }
}
