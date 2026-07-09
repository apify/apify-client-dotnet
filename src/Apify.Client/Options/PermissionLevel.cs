using System;

namespace Apify.Client.Options;

/// <summary>
/// The permission level an Actor run is granted (the <c>forcePermissionLevel</c> start parameter).
/// </summary>
public enum PermissionLevel
{
    /// <summary>The run gets a limited, scoped token (recommended for untrusted Actors).</summary>
    LimitedPermissions,

    /// <summary>The run gets a full-access token to the user's account.</summary>
    FullPermissions,
}

/// <summary>Maps <see cref="PermissionLevel"/> to its API wire representation.</summary>
internal static class PermissionLevelExtensions
{
    /// <summary>The wire value the API expects for the <c>forcePermissionLevel</c> parameter.</summary>
    public static string ToWireValue(this PermissionLevel level) => level switch
    {
        PermissionLevel.LimitedPermissions => "LIMITED_PERMISSIONS",
        PermissionLevel.FullPermissions => "FULL_PERMISSIONS",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "unknown permission level"),
    };
}
