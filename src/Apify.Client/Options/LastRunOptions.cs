namespace Apify.Client.Options;

/// <summary>
/// Filters which "last" run the last-run accessors resolve to. Leave a field <c>null</c> to leave that
/// filter unset.
/// </summary>
/// <remarks>
/// <c>Origin</c> is an Apify-platform convenience exposed by the reference client but not documented as a
/// query parameter in the OpenAPI spec; it is included for parity, threaded to the same <c>runs/last</c>
/// endpoint.
/// </remarks>
public sealed class LastRunOptions
{
    /// <summary>Filter by run status (e.g. <c>SUCCEEDED</c>, <c>FAILED</c>, <c>RUNNING</c>).</summary>
    public string? Status { get; init; }

    /// <summary>Filter by how the run was started (e.g. <c>DEVELOPMENT</c>, <c>WEB</c>, <c>API</c>).</summary>
    public string? Origin { get; init; }
}
