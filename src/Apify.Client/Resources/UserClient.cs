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
/// For the current user (<c>me</c>), it also exposes account usage and limits. Those endpoints only exist
/// for <c>me</c> and throw <see cref="System.InvalidOperationException"/> if called on another user's client.
/// </remarks>
public sealed class UserClient
{
    private const string Me = "me";

    private readonly HttpClientCore _http;
    private readonly ResourceContext _ctx;
    private readonly bool _isMe;

    internal UserClient(HttpClientCore http, string baseUrl, string id)
    {
        _http = http;
        _ctx = ResourceContext.Single(http, baseUrl, "users", id);
        _isMe = id == Me;
    }

    /// <summary>
    /// Fetches the user. For <c>me</c> it returns private account details (via
    /// <see cref="ApifyResource.ToJsonObject"/>); for other users it returns the public profile. Returns
    /// <c>null</c> if the user does not exist.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<User?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new User(obj) : null;
    }

    /// <summary>
    /// Fetches the current account's monthly usage for the month containing the given date (formatted as
    /// <c>YYYY-MM-DD</c>). An empty/<c>null</c> date reports the current month. Only available for <c>me</c>.
    /// </summary>
    /// <param name="date">The date whose month to report, or <c>null</c> for the current month.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonObject> MonthlyUsageAsync(string? date = null, CancellationToken cancellationToken = default)
    {
        RequireMe();
        var q = new QueryParams();
        if (!string.IsNullOrEmpty(date))
        {
            q.AddString("date", date);
        }

        var data = await _ctx.GetResourceRequiredAsync("usage/monthly", q, cancellationToken).ConfigureAwait(false);
        return data as JsonObject ?? new JsonObject();
    }

    /// <summary>Fetches the current account's resource limits. Only available for <c>me</c>.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<JsonObject> LimitsAsync(CancellationToken cancellationToken = default)
    {
        RequireMe();
        var data = await _ctx.GetResourceRequiredAsync("limits", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data as JsonObject ?? new JsonObject();
    }

    /// <summary>Updates the current account's resource limits. Only available for <c>me</c>.</summary>
    /// <param name="newLimits">Any JSON-serializable limits object.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task UpdateLimitsAsync(object newLimits, CancellationToken cancellationToken = default)
    {
        RequireMe();
        using var response = await _http.CallAsync(
            HttpMethod.Put,
            _ctx.SubUrl("limits"),
            Json.Encode(newLimits),
            ResourceContext.ContentTypeJson,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private void RequireMe()
    {
        if (!_isMe)
        {
            throw new System.InvalidOperationException("this operation is only available for the current user (use Me())");
        }
    }
}
