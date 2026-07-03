using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using Apify.Client.Internal;
using Apify.Client.Models;

namespace Apify.Client.Resources;

/// <summary>A client for a specific schedule (<c>/v2/schedules/{scheduleId}</c>).</summary>
public sealed class ScheduleClient
{
    private readonly ResourceContext _ctx;

    internal ScheduleClient(HttpClientCore http, string baseUrl, string id)
    {
        _ctx = ResourceContext.Single(http, baseUrl, "schedules", id);
    }

    /// <summary>Fetches the schedule, or <c>null</c> if it does not exist.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Schedule?> GetAsync(CancellationToken cancellationToken = default)
    {
        var data = await _ctx.GetResourceAsync("", new QueryParams(), cancellationToken).ConfigureAwait(false);
        return data is JsonObject obj ? new Schedule(obj) : null;
    }

    /// <summary>Updates the schedule with the given fields and returns the updated object.</summary>
    /// <param name="newFields">Any JSON-serializable set of fields to update.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public async Task<Schedule> UpdateAsync(object newFields, CancellationToken cancellationToken = default)
    {
        return new Schedule(await _ctx.UpdateResourceAsync("", newFields, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Deletes the schedule.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task DeleteAsync(CancellationToken cancellationToken = default) => _ctx.DeleteResourceAsync("", cancellationToken);

    /// <summary>Fetches the schedule's invocation log as text, or <c>null</c> if absent.</summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    public Task<string?> GetLogAsync(CancellationToken cancellationToken = default)
        => _ctx.GetRawAsync("log", new QueryParams(), cancellationToken);
}
