namespace Apify.Client.Options;

/// <summary>
/// Filters which "last" run the last-run accessors resolve to. Leave a field <c>null</c> to leave that
/// filter unset.
/// </summary>
/// <remarks>
/// <c>Status</c> and <c>Origin</c> are both spec-declared query parameters on the last-run endpoints
/// (<c>GET /v2/actors/{actorId}/runs/last</c> and <c>GET /v2/actor-tasks/{actorTaskId}/runs/last</c>),
/// matching the reference client's <c>lastRun({ status, origin })</c>. The spec also declares
/// <c>waitForFinish</c> on those endpoints, but the reference client does not expose it on <c>lastRun</c>,
/// so it is intentionally omitted here for parity.
/// </remarks>
public sealed class LastRunOptions
{
    /// <summary>Filter by run status (e.g. <c>SUCCEEDED</c>, <c>FAILED</c>, <c>RUNNING</c>).</summary>
    public string? Status { get; init; }

    /// <summary>Filter by how the run was started (e.g. <c>DEVELOPMENT</c>, <c>WEB</c>, <c>API</c>).</summary>
    public string? Origin { get; init; }
}
