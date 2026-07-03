using System.Threading;
using System.Threading.Tasks;
using Apify.Client.Internal;
using Apify.Client.Models;
using Apify.Client.Options;

namespace Apify.Client.Resources;

/// <summary>A client for the schedule collection (<c>GET/POST /v2/schedules</c>).</summary>
public sealed class ScheduleCollectionClient
{
    private readonly ResourceContext _ctx;

    internal ScheduleCollectionClient(HttpClientCore http, string baseUrl)
    {
        _ctx = ResourceContext.Collection(http, baseUrl, "schedules");
    }

    /// <summary>Lists the account's schedules.</summary>
    /// <param name="options">Optional pagination.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<PaginationList<Schedule>> ListAsync(ListOptions? options = null, CancellationToken cancellationToken = default)
    {
        var q = new QueryParams();
        (options ?? new ListOptions()).AppendTo(q);
        return _ctx.ListResourceAsync("", q, static d => new Schedule(d), cancellationToken);
    }

    /// <summary>Creates a new schedule.</summary>
    /// <param name="schedule">Any JSON-serializable schedule definition.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Schedule> CreateAsync(object schedule, CancellationToken cancellationToken = default)
    {
        return new Schedule(await _ctx.CreateResourceAsync(new QueryParams(), schedule, cancellationToken).ConfigureAwait(false));
    }
}
