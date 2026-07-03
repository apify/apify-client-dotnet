using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>A single version of an Actor.</summary>
public sealed class ActorVersion : ApifyResource
{
    /// <summary>Wraps a raw version object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public ActorVersion(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The version identifier (e.g. <c>0.1</c>).</summary>
    public string? VersionNumber => GetString("versionNumber");

    /// <summary>How the version's source is provided (e.g. <c>SOURCE_FILES</c>).</summary>
    public string? SourceType => GetString("sourceType");
}
