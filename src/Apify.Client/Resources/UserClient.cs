using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>
/// A client for accessing user data (<c>/v2/users/{userId}</c> or <c>/v2/users/me</c>).
/// </summary>
/// <remarks>
/// It also exposes the user's monthly usage and account limits. The API serves those for <c>me</c> and for
/// the authenticated user's own ID; asking for another user's data is rejected by the API.
/// </remarks>
public sealed class UserClient
{
    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;

    internal UserClient(HttpClientCore http, string baseUrl, string id)
    {
        _http = http;
        _ctx = ResourceContext.Single(http, baseUrl, "users", id);
    }

    /// <summary>
    /// Fetches the user. For <c>me</c> it returns private account details (via
    /// <see cref="ApifyResource.ToJsonObject"/>); for other users it returns the public profile. Returns
    /// <c>null</c> if the user does not exist.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<User?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new User(obj) : null;
    }

    /// <summary>
    /// Fetches the user's monthly usage for the month containing the given date (formatted as
    /// <c>YYYY-MM-DD</c>). An empty/<c>null</c> date reports the current month.
    /// </summary>
    /// <param name="date">The date whose month to report, or <c>null</c> for the current month.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonObject> MonthlyUsageAsync(string? date = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        if (!string.IsNullOrEmpty(date))
        {
            q.AddString("date", date);
        }

        return await _ctx.GetResourceRequiredAsync("usage/monthly", q, RequestTier.Short, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Fetches the user's account and usage limits.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonObject> LimitsAsync(CancellationToken cancellationToken = default)
    {
        return await _ctx.GetResourceRequiredAsync("limits", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Updates the user's account limits.</summary>
    /// <param name="newLimits">Any JSON-serializable limits object.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task UpdateLimitsAsync(object newLimits, CancellationToken cancellationToken = default)
    {
        using var response = await _http.CallAsync(
            HttpMethod.Put,
            _ctx.SubUrl("limits"),
            Json.Encode(newLimits),
            ResourceContext.ContentTypeJson,
            timeout: _ctx.RequestTimeout,
            tier: RequestTier.Short,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns this client with every subsequent call's timeout set to <paramref name="timeout"/>,
    /// overriding the tier default (see the "Timeout tiers" section of the top-level README). Pass
    /// <see cref="TimeSpan.Zero"/> for no timeout, matching the reference client's <c>'noTimeout'</c>. A
    /// value above <see cref="ApifyClientOptions.TimeoutSecs"/> (the overall budget) is capped at it —
    /// raise <see cref="ApifyClientOptions.TimeoutSecs"/> itself to allow a longer per-call timeout.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public UserClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
