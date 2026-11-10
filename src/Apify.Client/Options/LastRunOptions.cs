namespace Apify.Client.Options;

/// <summary>Filters which last run the last-run accessors resolve to. A <c>null</c> field leaves that filter unset.</summary>
/// <remarks>
/// <c>Status</c> and <c>Origin</c> are the query parameters of the last-run endpoints, as in the reference client's
/// <c>lastRun({ status, origin })</c>. <c>waitForFinish</c> is left out because the reference client does not expose it
/// there.
/// </remarks>
public sealed class LastRunOptions
{
    /// <summary>Filter by run status (e.g. <c>SUCCEEDED</c>, <c>FAILED</c>, <c>RUNNING</c>).</summary>
    public string? Status { get; init; }

    /// <summary>Filter by how the run was started (e.g. <c>DEVELOPMENT</c>, <c>WEB</c>, <c>API</c>).</summary>
    public string? Origin { get; init; }
}
