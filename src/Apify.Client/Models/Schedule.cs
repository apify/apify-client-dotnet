using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>A schedule automatically starts Actor or task runs at specified times.</summary>
public sealed class Schedule : ApifyResource
{
    /// <summary>Wraps a raw schedule object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public Schedule(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique schedule ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The ID of the user who owns the schedule.</summary>
    public string? UserId => GetString("userId");

    /// <summary>The schedule name.</summary>
    public string? Name => GetString("name");

    /// <summary>The cron expression governing when the schedule fires.</summary>
    public string? CronExpression => GetString("cronExpression");

    /// <summary>Whether the schedule is currently active.</summary>
    public bool? IsEnabled => GetBool("isEnabled");

    /// <summary>The human-readable title of the schedule, if one was set.</summary>
    public string? Title => GetString("title");

    /// <summary>A description of the schedule, if one was set.</summary>
    public string? Description => GetString("description");

    /// <summary>
    /// The state of the Console's schedule editor, or <c>null</c> once an API update changed the
    /// <see cref="CronExpression"/> or the timezone and the editor state was dropped.
    /// </summary>
    public JsonObject? UiState => Get("uiState") as JsonObject;
}
