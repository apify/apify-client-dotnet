using Apify.Client.Internal;

namespace Apify.Client.Options;

/// <summary>Configures building an Actor version.</summary>
public sealed class ActorBuildOptions
{
    /// <summary>If <c>true</c>, use beta versions of Apify packages.</summary>
    public bool? BetaPackages { get; init; }

    /// <summary>The tag to apply to the build (e.g. <c>latest</c>).</summary>
    public string? Tag { get; init; }

    /// <summary>Whether to use the Docker build cache (default true).</summary>
    public bool? UseCache { get; init; }

    /// <summary>Maximum seconds to wait server-side for the build (max 60).</summary>
    public int? WaitForFinish { get; init; }

    internal void AppendTo(QueryParams q)
    {
        q.AddBool("betaPackages", BetaPackages)
            .AddString("tag", Tag)
            .AddBool("useCache", UseCache)
            .AddInt("waitForFinish", WaitForFinish);
    }
}
