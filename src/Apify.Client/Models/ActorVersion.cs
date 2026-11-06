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

    /// <summary>
    /// The Actor's directory inside the uploaded source of a <c>SOURCE_FILES</c> or <c>TARBALL</c> version, as a
    /// path relative to the upload root with <c>/</c> separators (for example <c>actors/typescript-actor</c>).
    /// <c>null</c> when the upload root is the Actor's directory, and absent for a <c>TARBALL</c> version when the
    /// caller has no write access to the Actor.
    /// </summary>
    public string? ActorPath => GetString("actorPath");
}
