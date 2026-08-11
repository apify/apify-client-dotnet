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
/// The entry point for interacting with the Apify API.
/// </summary>
/// <remarks>
/// <para>
/// See the project's top-level README for the client's official-but-experimental status.
/// </para>
/// <para>
/// Construct it with an API token (and optional settings via <see cref="ApifyClientOptions"/>), then
/// obtain resource clients via the accessor methods, e.g. <see cref="Actor"/>, <see cref="Dataset"/>,
/// <see cref="Run"/>.
/// </para>
/// <para>
/// <b>Architecture.</b> The public interface is this class and the resource clients it returns. The
/// replaceable transport is the <see cref="IHttpTransport"/> (default <see cref="HttpClientTransport"/>);
/// pass a custom one via <see cref="ApifyClientOptions.HttpTransport"/>. Cross-cutting behaviour (auth,
/// User-Agent, retries with exponential backoff, timeouts) lives in the internal HTTP client and is
/// applied to every request.
/// </para>
/// </remarks>
public sealed class ApifyClient
{
    /// <summary>Default base URL of the Apify API (without the <c>/v2</c> suffix).</summary>
    public const string DefaultBaseUrl = "https://api.apify.com";

    /// <summary>Default maximum number of retries for failed requests.</summary>
    public const int DefaultMaxRetries = 8;

    /// <summary>Default minimum delay between retries, in milliseconds.</summary>
    public const int DefaultMinDelayMillis = 500;

    /// <summary>Default overall per-request timeout, in seconds.</summary>
    public const int DefaultTimeoutSecs = 360;

    /// <summary>Environment variable that signals the client is running on the Apify platform.</summary>
    private const string EnvIsAtHome = "APIFY_IS_AT_HOME";

    /// <summary>Environment variable holding the current Actor run's id (set on the platform).</summary>
    private const string EnvActorRunId = "ACTOR_RUN_ID";

    /// <summary>Addresses the current user (<c>/users/me</c>).</summary>
    private const string MeUserPlaceholder = "me";

    private readonly HttpClientCore _http;
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
            options.TimeoutSecs);

        var userAgent = BuildUserAgent(options.UserAgentSuffix, options.IsAtHome ?? DefaultIsAtHome);
        _http = new HttpClientCore(transport, options.Token, userAgent, retry, options.RequestCompression);

        _baseUrl = TrimTrailingSlash(options.BaseUrl) + "/v2";
        var publicSource = options.PublicBaseUrl ?? options.BaseUrl;
        _publicBaseUrl = TrimTrailingSlash(publicSource) + "/v2";
    }

    /// <summary>The <c>User-Agent</c> header value this client sends.</summary>
    public string UserAgent => _http.UserAgent;

    /// <summary>The fully-qualified API base URL this client targets (including the <c>/v2</c> suffix).</summary>
    public string ApiBaseUrl => _baseUrl;

    // ----- Actor accessors -----------------------------------------------------

    /// <summary>A client for the Actor collection (list &amp; create Actors).</summary>
    public ActorCollectionClient Actors() => new(_http, _baseUrl);

    /// <summary>A client for a specific Actor, addressed by ID or <c>username~name</c>.</summary>
    /// <param name="id">The Actor ID or <c>username~name</c>.</param>
    public ActorClient Actor(string id) => new(this, _http, _baseUrl, id);

    // ----- Build accessors -----------------------------------------------------

    /// <summary>A client for the Actor build collection (list builds).</summary>
    public BuildCollectionClient Builds() => new(_http, _baseUrl, "actor-builds");

    /// <summary>A client for a specific Actor build.</summary>
    /// <param name="id">The build ID.</param>
    public BuildClient Build(string id) => new(_http, _baseUrl, id);

    // ----- Run accessors -------------------------------------------------------

    /// <summary>A client for the Actor run collection (list runs).</summary>
    public RunCollectionClient Runs() => new(_http, _baseUrl, "actor-runs");

    /// <summary>A client for a specific Actor run.</summary>
    /// <param name="id">The run ID.</param>
    public RunClient Run(string id) => new(_http, _baseUrl, "actor-runs", id);

    // ----- Dataset accessors ---------------------------------------------------

    /// <summary>A client for the dataset collection (list &amp; get-or-create datasets).</summary>
    public DatasetCollectionClient Datasets() => new(_http, _baseUrl);

    /// <summary>A client for a specific dataset, addressed by ID or name.</summary>
    /// <param name="id">The dataset ID or name.</param>
    public DatasetClient Dataset(string id) => DatasetClient.ForId(_http, _baseUrl, id).WithPublicBase(_publicBaseUrl);

    // ----- Key-value store accessors -------------------------------------------

    /// <summary>A client for the key-value store collection.</summary>
    public KeyValueStoreCollectionClient KeyValueStores() => new(_http, _baseUrl);

    /// <summary>A client for a specific key-value store, addressed by ID or name.</summary>
    /// <param name="id">The store ID or name.</param>
    public KeyValueStoreClient KeyValueStore(string id) => KeyValueStoreClient.ForId(_http, _baseUrl, id).WithPublicBase(_publicBaseUrl);

    // ----- Request queue accessors ---------------------------------------------

    /// <summary>A client for the request queue collection.</summary>
    public RequestQueueCollectionClient RequestQueues() => new(_http, _baseUrl);

    /// <summary>
    /// A client for a specific request queue, addressed by ID or name. Optionally pass options to set a
    /// stable <c>ClientKey</c> and/or a per-request <c>TimeoutSecs</c> for this queue's calls.
    /// </summary>
    /// <param name="id">The queue ID or name.</param>
    /// <param name="options">Optional per-queue-client options.</param>
    public RequestQueueClient RequestQueue(string id, Options.RequestQueueClientOptions? options = null)
        => RequestQueueClient.ForId(_http, _baseUrl, id, options);

    // ----- Task accessors ------------------------------------------------------

    /// <summary>A client for the Actor task collection (list &amp; create tasks).</summary>
    public TaskCollectionClient Tasks() => new(_http, _baseUrl);

    /// <summary>A client for a specific Actor task.</summary>
    /// <param name="id">The task ID.</param>
    public TaskClient Task(string id) => new(this, _http, _baseUrl, id);

    // ----- Schedule accessors --------------------------------------------------

    /// <summary>A client for the schedule collection (list &amp; create schedules).</summary>
    public ScheduleCollectionClient Schedules() => new(_http, _baseUrl);

    /// <summary>A client for a specific schedule.</summary>
    /// <param name="id">The schedule ID.</param>
    public ScheduleClient Schedule(string id) => new(_http, _baseUrl, id);

    // ----- Webhook accessors ---------------------------------------------------

    /// <summary>A client for the webhook collection (list &amp; create webhooks).</summary>
    public WebhookCollectionClient Webhooks() => new(_http, _baseUrl);

    /// <summary>A client for a specific webhook.</summary>
    /// <param name="id">The webhook ID.</param>
    public WebhookClient Webhook(string id) => new(_http, _baseUrl, id);

    /// <summary>A client for the webhook dispatch collection.</summary>
    public WebhookDispatchCollectionClient WebhookDispatches() => new(_http, _baseUrl, "webhook-dispatches");

    /// <summary>A client for a specific webhook dispatch.</summary>
    /// <param name="id">The dispatch ID.</param>
    public WebhookDispatchClient WebhookDispatch(string id) => new(_http, _baseUrl, id);

    // ----- Misc accessors ------------------------------------------------------

    /// <summary>A client for browsing the Apify Store.</summary>
    public StoreCollectionClient Store() => new(_http, _baseUrl);

    /// <summary>A client for accessing a build's or run's log.</summary>
    /// <param name="buildOrRunId">The build or run ID.</param>
    public LogClient Log(string buildOrRunId) => LogClient.ForId(_http, _baseUrl, buildOrRunId);

    /// <summary>A client for the current user (<c>/users/me</c>).</summary>
    public UserClient Me() => new(_http, _baseUrl, MeUserPlaceholder);

    /// <summary>A client for a specific user by ID or username.</summary>
    /// <param name="id">The user ID or username.</param>
    public UserClient User(string id) => new(_http, _baseUrl, id);

    /// <summary>
    /// Sets the status message of the current Actor run.
    /// </summary>
    /// <remarks>
    /// This convenience method updates the run identified by the <c>ACTOR_RUN_ID</c> environment variable,
    /// so it only works when called from inside an Actor run. If <paramref name="isTerminal"/> is true, the
    /// message becomes final and won't be overwritten. Throws <see cref="InvalidOperationException"/> if
    /// <c>ACTOR_RUN_ID</c> is not set.
    /// </remarks>
    /// <param name="message">The status message to set.</param>
    /// <param name="isTerminal">Whether the message is final.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
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
    /// Reports whether the client is running on the Apify platform, by reading the <c>APIFY_IS_AT_HOME</c>
    /// environment variable (set to a non-empty value on the platform).
    /// </summary>
    private static bool DefaultIsAtHome()
    {
        var value = Environment.GetEnvironmentVariable(EnvIsAtHome);
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Builds the <c>User-Agent</c> header value mandated by the client requirements:
    /// <c>ApifyClient/{version} ({os}; .NET/{runtimeVersion}); isAtHome/{true|false}</c>.
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
    /// Additional Node <c>os.platform()</c> tokens for Unix platforms that .NET has no dedicated
    /// <see cref="OperatingSystem"/> helper for, matched via <see cref="RuntimeInformation.IsOSPlatform"/>.
    /// Solaris and illumos both report as <c>sunos</c>, matching Node.
    /// </summary>
    /// <remarks>
    /// These entries are deliberately kept even though .NET has no officially supported runtime on these
    /// platforms today: the requirement is that every Apify client emit the exact same OS token as the
    /// reference client's <c>os.platform()</c>, so should .NET ever run there the token stays aligned
    /// rather than degrading to <c>unknown</c>. They are a forward-compatible superset, not live branches.
    /// </remarks>
    private static readonly (OSPlatform Platform, string Token)[] ExtendedOsTokens =
    {
        (OSPlatform.Create("OPENBSD"), "openbsd"),
        (OSPlatform.Create("NETBSD"), "netbsd"),
        (OSPlatform.Create("SOLARIS"), "sunos"),
        (OSPlatform.Create("ILLUMOS"), "sunos"),
        (OSPlatform.Create("AIX"), "aix"),
    };

    /// <summary>Resolves the current platform to its <c>User-Agent</c> OS token (see <see cref="ResolveOsToken"/>).</summary>
    private static string CurrentOs() => ResolveOsToken(
        OperatingSystem.IsWindows(),
        OperatingSystem.IsMacOS(),
        OperatingSystem.IsAndroid(),
        OperatingSystem.IsLinux(),
        OperatingSystem.IsFreeBSD(),
        RuntimeInformation.IsOSPlatform);

    /// <summary>
    /// Maps the detected runtime platform to the short, lowercase token used in the <c>User-Agent</c> OS
    /// field. The tokens are exactly the reference JS client's Node <c>os.platform()</c> values
    /// (<c>win32</c>, <c>darwin</c>, <c>android</c>, <c>linux</c>, <c>freebsd</c>, <c>openbsd</c>,
    /// <c>netbsd</c>, <c>sunos</c>, <c>aix</c>), so the token is identical across Apify clients. Android is
    /// checked before Linux because an Android runtime is also Linux-based. The five platforms .NET exposes
    /// dedicated <see cref="OperatingSystem"/> helpers for are matched first; the remaining Unix platforms
    /// Node names but .NET has no helper for are matched via <paramref name="isOsPlatform"/>. Platforms the
    /// reference's Node runtime cannot run on at all (e.g. iOS, the browser) have no reference token and so
    /// fall back to <c>unknown</c>.
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
