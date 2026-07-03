using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures Actor input validation. All fields are optional.</summary>
public sealed class ValidateInputOptions
{
    /// <summary>The tag or number of the build whose input schema is used for validation.</summary>
    public string? Build { get; init; }

    /// <summary>The content type of the input body. Defaults to <c>application/json</c>.</summary>
    public string? ContentType { get; init; }

    /// <summary>The configured content type, or the JSON default when unset.</summary>
    internal string ContentTypeOrDefault() =>
        string.IsNullOrEmpty(ContentType) ? ResourceContext.ContentTypeJson : ContentType;

    internal void AppendTo(QueryParams q)
    {
        q.AddString("build", Build);
    }
}
