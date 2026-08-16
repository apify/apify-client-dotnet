namespace Apify.Client;

/// <summary>
/// Public version constants for the Apify .NET client.
/// </summary>
/// <remarks>
/// <see cref="ClientVersion"/> is the semantic version of this library and
/// <see cref="ApiSpecVersion"/> is the <c>info.version</c> of the Apify OpenAPI specification this
/// client was generated and verified against.
/// </remarks>
public static class ApifyClientVersion
{
    /// <summary>
    /// The semantic version of this client library (see https://semver.org/). Changes to the public
    /// interface other than additive ones are considered breaking changes.
    /// </summary>
    public const string ClientVersion = "0.3.1";

    /// <summary>
    /// The version of the Apify OpenAPI specification this client was generated and verified against.
    /// Corresponds to the <c>info.version</c> field of the Apify OpenAPI document.
    /// </summary>
    public const string ApiSpecVersion = "v2-2026-08-14T072928Z";
}
