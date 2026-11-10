using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Http;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Resources;

namespace Apify.Client;

/// <summary>
/// The entry point of the client: create it with an API token (and optionally <see cref="ApifyClientOptions"/>), then
/// get resource clients from its accessors, such as <see cref="Actor"/>, <see cref="Dataset"/> and <see cref="Run"/>.
/// </summary>
/// <remarks>
/// Authentication, the <c>User-Agent</c> header, retries with exponential backoff and timeouts are applied to every
/// request. Pass a custom <see cref="IHttpTransport"/> in <see cref="ApifyClientOptions.HttpTransport"/> to replace
/// the default <see cref="HttpClientTransport"/>.
/// </remarks>
public sealed class ApifyClient
{
    /// <summary>Default base URL of the Apify API (without the <c>/v2</c> suffix).</summary>
    public const string DefaultBaseUrl = "https://api.apify.com";

    /// <summary>Default maximum number of retries for failed requests.</summary>
    public const int DefaultMaxRetries = 8;

    /// <summary>Default minimum delay between retries, in milliseconds.</summary>
    public const int DefaultMinDelayMillis = 500;

    /// <summary>Default overall per-request timeout, in seconds (also the default <c>Long</c> tier duration).</summary>
    public const int DefaultTimeoutSecs = 360;

    /// <summary>Default duration of the <c>Short</c> timeout tier, in seconds.</summary>
    public const int DefaultTimeoutShortSecs = 5;

    /// <summary>Default duration of the <c>Medium</c> timeout tier, in seconds.</summary>
    public const int DefaultTimeoutMediumSecs = 30;

    /// <summary>Set to a non-empty value when the client runs on the Apify platform.</summary>
    private const string EnvIsAtHome = "APIFY_IS_AT_HOME";

    /// <summary>The ID of the current Actor run, set on the platform.</summary>
    private const string EnvActorRunId = "ACTOR_RUN_ID";

    /// <summary>Addresses the current user (<c>/users/me</c>).</summary>
    private const string MeUserPlaceholder = "me";

    private readonly HttpClientCore _http;
    private readonly Statistics _stats = new();
    private readonly string _baseUrl;
    private readonly string _publicBaseUrl;

    /// <summary>Creates a client with the given API token and otherwise default settings.</summary>
    /// <param name="token">API token, sent as a Bearer token.</param>
    public ApifyClient(string? token = null)
        : this(new ApifyClientOptions { Token = token })
    {
    }

    /// <summary>Creates a client from the given options.</summary>
    /// <param name="options">The client configuration.</param>
    public ApifyClient(ApifyClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var transport = options.HttpTransport ?? new HttpClientTransport();
        var maxDelayMillis = options.MaxDelayBetweenRetriesMillis ?? (options.TimeoutSecs * 1000);
        var retry = new RetryConfig(
            options.MaxRetries,
            options.MinDelayBetweenRetriesMillis,
            maxDelayMillis,
            options.TimeoutSecs,
            options.TimeoutLongSecs ?? options.TimeoutSecs,
            options.TimeoutShortSecs,
            options.TimeoutMediumSecs);

        var userAgent = BuildUserAgent(options.UserAgentSuffix, options.IsAtHome ?? DefaultIsAtHome);
        _http = new HttpClientCore(transport, options.Token, userAgent, retry, ResolveCompressor(options), _stats, options.Headers);

        _baseUrl = ToApiBaseUrl(options.BaseUrl);
        _publicBaseUrl = ToApiBaseUrl(options.PublicBaseUrl ?? DefaultBaseUrl);
    }

    /// <summary>
    /// The compressor for large request bodies: <see cref="ApifyClientOptions.Compressor"/>, or the built-in one for
    /// <see cref="ApifyClientOptions.RequestCompression"/>.
    /// </summary>
    private static HttpCompressor ResolveCompressor(ApifyClientOptions options)
    {
        if (options.Compressor is not null)
        {
            return options.Compressor;
        }

        return options.RequestCompression switch
        {
            RequestCompression.Gzip => new GzipHttpCompressor(),
            RequestCompression.Zstd => new ZstdHttpCompressor(),
            _ => new BrotliHttpCompressor(),
        };
    }

    /// <summary>The API version path, appended to a base URL that does not end with it.</summary>
    private const string ApiVersionPath = "/v2";

    /// <summary>
    /// Normalizes a base URL to end with <see cref="ApiVersionPath"/>: trailing slashes are stripped and the path is
    /// appended unless the URL already ends with <c>/v2</c>. Another version segment such as <c>/v3</c> is kept as part of
    /// the host path, since the endpoints only exist under v2.
    /// </summary>
    private static string ToApiBaseUrl(string url)
    {
        var trimmed = TrimTrailingSlash(url);
        return trimmed.EndsWith(ApiVersionPath, StringComparison.Ordinal) ? trimmed : trimmed + ApiVersionPath;
    }

    /// <summary>
    /// The <c>User-Agent</c> this client sends: the built-in one, or the <c>User-Agent</c> entry of
    /// <see cref="ApifyClientOptions.Headers"/> when it overrides it.
    /// </summary>
    public string UserAgent => _http.UserAgent;

    /// <summary>Counters of the calls and requests this client made and the rate limit errors it met.</summary>
    public Statistics Stats => _stats;

    /// <summary>The API base URL this client targets, including the <c>/v2</c> suffix.</summary>
    public string ApiBaseUrl => _baseUrl;

    /// <summary>A client for the Actor collection (list &amp; create Actors).</summary>
    public ActorCollectionClient Actors() => new(_http, _baseUrl);

    /// <summary>A client for a specific Actor, addressed by ID or <c>username~name</c>. Must not be empty.</summary>
    /// <param name="id">The Actor ID or <c>username~name</c>.</param>
    public ActorClient Actor(string id) => new(this, _http, _baseUrl, RequireNonEmptyId(id, nameof(id)));

    /// <summary>A client for the Actor build collection (list builds).</summary>
    public BuildCollectionClient Builds() => new(_http, _baseUrl, "actor-builds");

    /// <summary>A client for a specific Actor build. Must not be empty.</summary>
    /// <param name="id">The build ID.</param>
    public BuildClient Build(string id) => new(_http, _baseUrl, RequireNonEmptyId(id, nameof(id)));

    /// <summary>A client for the Actor run collection (list runs).</summary>
    public RunCollectionClient Runs() => new(_http, _baseUrl, "actor-runs");

    /// <summary>A client for a specific Actor run. Must not be empty.</summary>
    /// <param name="id">The run ID.</param>
    public RunClient Run(string id) => new(this, _http, _baseUrl, "actor-runs", RequireNonEmptyId(id, nameof(id)));

    /// <summary>A client for the dataset collection (list &amp; get-or-create datasets).</summary>
    public DatasetCollectionClient Datasets() => new(_http, _baseUrl);

    /// <summary>A client for a specific dataset, addressed by ID or name. Must not be empty.</summary>
    /// <param name="id">The dataset ID or name.</param>
    public DatasetClient Dataset(string id) => DatasetClient.ForId(_http, _baseUrl, RequireNonEmptyId(id, nameof(id))).WithPublicBase(_publicBaseUrl);

    /// <summary>A client for the key-value store collection.</summary>
    public KeyValueStoreCollectionClient KeyValueStores() => new(_http, _baseUrl);

    /// <summary>A client for a specific key-value store, addressed by ID or name. Must not be empty.</summary>
    /// <param name="id">The store ID or name.</param>
    public KeyValueStoreClient KeyValueStore(string id) => KeyValueStoreClient.ForId(_http, _baseUrl, RequireNonEmptyId(id, nameof(id))).WithPublicBase(_publicBaseUrl);

    /// <summary>A client for the request queue collection.</summary>
    public RequestQueueCollectionClient RequestQueues() => new(_http, _baseUrl);

    /// <summary>
    /// A client for a specific request queue, addressed by ID or name. Must not be empty. The options set a stable
    /// <c>ClientKey</c> and a per-request <c>TimeoutSecs</c> for this queue's calls.
    /// </summary>
    /// <param name="id">The queue ID or name.</param>
    /// <param name="options">Optional per-queue-client options.</param>
    public RequestQueueClient RequestQueue(string id, Options.RequestQueueClientOptions? options = null)
        => RequestQueueClient.ForId(_http, _baseUrl, RequireNonEmptyId(id, nameof(id)), options);

    /// <summary>A client for the Actor task collection (list &amp; create tasks).</summary>
    public TaskCollectionClient Tasks() => new(_http, _baseUrl);

    /// <summary>A client for a specific Actor task. Must not be empty.</summary>
    /// <param name="id">The task ID.</param>
    public TaskClient Task(string id) => new(this, _http, _baseUrl, RequireNonEmptyId(id, nameof(id)));

    /// <summary>A client for the schedule collection (list &amp; create schedules).</summary>
    public ScheduleCollectionClient Schedules() => new(_http, _baseUrl);

    /// <summary>A client for a specific schedule. Must not be empty.</summary>
    /// <param name="id">The schedule ID.</param>
    public ScheduleClient Schedule(string id) => new(_http, _baseUrl, RequireNonEmptyId(id, nameof(id)));

    /// <summary>A client for the webhook collection (list &amp; create webhooks).</summary>
    public WebhookCollectionClient Webhooks() => new(_http, _baseUrl);

    /// <summary>A client for a specific webhook. Must not be empty.</summary>
    /// <param name="id">The webhook ID.</param>
    public WebhookClient Webhook(string id) => new(_http, _baseUrl, RequireNonEmptyId(id, nameof(id)));

    /// <summary>A client for the webhook dispatch collection.</summary>
    public WebhookDispatchCollectionClient WebhookDispatches() => new(_http, _baseUrl, "webhook-dispatches");

    /// <summary>A client for a specific webhook dispatch. Must not be empty.</summary>
    /// <param name="id">The dispatch ID.</param>
    public WebhookDispatchClient WebhookDispatch(string id) => new(_http, _baseUrl, RequireNonEmptyId(id, nameof(id)));

    /// <summary>A client for browsing the Apify Store.</summary>
    public StoreCollectionClient Store() => new(_http, _baseUrl);

    /// <summary>A client for accessing a build's or run's log. Must not be empty.</summary>
    /// <param name="buildOrRunId">The build or run ID.</param>
    public LogClient Log(string buildOrRunId) => LogClient.ForId(_http, _baseUrl, RequireNonEmptyId(buildOrRunId, nameof(buildOrRunId)));

    /// <summary>A client for the current user (<c>/users/me</c>).</summary>
    public UserClient Me() => new(_http, _baseUrl, MeUserPlaceholder);

    /// <summary>A client for a specific user by ID or username. Must not be empty.</summary>
    /// <param name="id">The user ID or username.</param>
    public UserClient User(string id) => new(_http, _baseUrl, RequireNonEmptyId(id, nameof(id)));

    /// <summary>
    /// Sets the status message of the current Actor run, the one in the <c>ACTOR_RUN_ID</c> environment variable. A
    /// terminal message is final and is not overwritten.
    /// </summary>
    /// <param name="message">The status message to set.</param>
    /// <param name="isTerminal">Whether the message is final.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="InvalidOperationException"><c>ACTOR_RUN_ID</c> is not set.</exception>
    public Task<ActorRun> SetStatusMessageAsync(string message, bool isTerminal = false, CancellationToken cancellationToken = default)
    {
        var runId = Environment.GetEnvironmentVariable(EnvActorRunId);
        if (string.IsNullOrEmpty(runId))
        {
            throw new InvalidOperationException("ACTOR_RUN_ID environment variable is not set");
        }

        var fields = new JsonObject
        {
            ["statusMessage"] = message,
            ["isStatusMessageTerminal"] = isTerminal,
        };
        return Run(runId).UpdateAsync(fields, cancellationToken);
    }

    private static string TrimTrailingSlash(string value) => value.TrimEnd('/');

    /// <summary>
    /// Rejects an empty ID, which would otherwise address a collection URL such as <c>.../actor-runs/</c>. Returns the
    /// ID so it can be used inline.
    /// </summary>
    private static string RequireNonEmptyId(string id, string paramName)
    {
        if (id.Length == 0)
        {
            throw new ArgumentException("id must not be empty", paramName);
        }

        return id;
    }

    /// <summary>Whether <c>APIFY_IS_AT_HOME</c> is set to a non-empty value.</summary>
    private static bool DefaultIsAtHome()
    {
        var value = Environment.GetEnvironmentVariable(EnvIsAtHome);
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Builds the <c>User-Agent</c> value <c>ApifyClient/{version} ({os}; .NET/{runtimeVersion}); isAtHome/{true|false}</c>.
    /// </summary>
    private static string BuildUserAgent(string? suffix, Func<bool> isAtHomeFn)
    {
        var os = CurrentOs();
        var atHome = isAtHomeFn() ? "true" : "false";
        var ua = string.Format(
            CultureInfo.InvariantCulture,
            "ApifyClient/{0} ({1}; .NET/{2}); isAtHome/{3}",
            ApifyClientVersion.ClientVersion,
            os,
            Environment.Version,
            atHome);
        if (!string.IsNullOrEmpty(suffix))
        {
            ua += "; " + suffix;
        }

        return ua;
    }

    /// <summary>
    /// OS tokens of Unix platforms that <see cref="OperatingSystem"/> has no helper for. Solaris and illumos both
    /// report <c>sunos</c>, as in Node.
    /// </summary>
    /// <remarks>
    /// .NET has no supported runtime on these platforms today. They keep the token the same as the reference client's
    /// <c>os.platform()</c> should one appear.
    /// </remarks>
    private static readonly (OSPlatform Platform, string Token)[] ExtendedOsTokens =
    {
        (OSPlatform.Create("OPENBSD"), "openbsd"),
        (OSPlatform.Create("NETBSD"), "netbsd"),
        (OSPlatform.Create("SOLARIS"), "sunos"),
        (OSPlatform.Create("ILLUMOS"), "sunos"),
        (OSPlatform.Create("AIX"), "aix"),
    };

    /// <summary>The <c>User-Agent</c> OS token of the current platform.</summary>
    private static string CurrentOs() => ResolveOsToken(
        OperatingSystem.IsWindows(),
        OperatingSystem.IsMacOS(),
        OperatingSystem.IsAndroid(),
        OperatingSystem.IsLinux(),
        OperatingSystem.IsFreeBSD(),
        RuntimeInformation.IsOSPlatform);

    /// <summary>
    /// Maps the platform to the OS token of the <c>User-Agent</c>: the values of Node's <c>os.platform()</c> (<c>win32</c>,
    /// <c>darwin</c>, <c>android</c>, <c>linux</c>, <c>freebsd</c>, <c>openbsd</c>, <c>netbsd</c>, <c>sunos</c>,
    /// <c>aix</c>), so the token is the same in every Apify client. Android is checked before Linux, which it is based on.
    /// A platform Node does not run on has no token and maps to <c>unknown</c>.
    /// </summary>
    internal static string ResolveOsToken(
        bool isWindows,
        bool isMacOs,
        bool isAndroid,
        bool isLinux,
        bool isFreeBsd,
        Func<OSPlatform, bool> isOsPlatform)
    {
        if (isWindows)
        {
            return "win32";
        }

        if (isMacOs)
        {
            return "darwin";
        }

        if (isAndroid)
        {
            return "android";
        }

        if (isLinux)
        {
            return "linux";
        }

        if (isFreeBsd)
        {
            return "freebsd";
        }

        foreach (var (platform, token) in ExtendedOsTokens)
        {
            if (isOsPlatform(platform))
            {
                return token;
            }
        }

        return "unknown";
    }
}
