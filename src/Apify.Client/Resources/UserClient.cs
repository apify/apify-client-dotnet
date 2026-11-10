using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>A client for a user: <c>me</c>, or another user by ID or username.</summary>
/// <remarks>
/// It also exposes the monthly usage and account limits. Besides <c>me</c>, the API accepts an ID or username only for
/// your own account or the organization your token belongs to. Tokens with limited permissions cannot use the by-ID
/// endpoints, and updating an organization's limits needs the permission to manage it.
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
    /// Fetches the user: private account details for <c>me</c> (via <see cref="ApifyResource.ToJsonObject"/>), the public
    /// profile for others. Returns <c>null</c> if the user does not exist.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<User?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), RequestTier.Short, cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new User(obj) : null;
    }

    /// <summary>Fetches the monthly usage for the month of <paramref name="date"/> (<c>YYYY-MM-DD</c>), by default the current month.</summary>
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
    /// Returns this client with the timeout of every subsequent call set to <paramref name="timeout"/>, overriding the
    /// tier default (see "Timeout tiers" in docs/README.md). <see cref="TimeSpan.Zero"/> means no timeout. A value above
    /// <see cref="ApifyClientOptions.TimeoutSecs"/> is capped at it.
    /// </summary>
    /// <param name="timeout">The timeout to use for every call made through this client.</param>
    public UserClient WithTimeout(TimeSpan timeout)
    {
        _ctx.WithTimeout(timeout);
        return this;
    }
}
