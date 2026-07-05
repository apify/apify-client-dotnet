using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures a run metamorph.</summary>
public sealed class MetamorphOptions
{
    /// <summary>Optionally pins the target Actor's build (unset for default).</summary>
    public string? Build { get; init; }

    /// <summary>The content type of the input body. Defaults to <c>application/json</c>.</summary>
    public string? ContentType { get; init; }

    /// <summary>The configured content type, or the JSON default when unset.</summary>
    internal string ContentTypeOrDefault() =>
        string.IsNullOrEmpty(ContentType) ? ResourceContext.ContentTypeJson : ContentType;
}
