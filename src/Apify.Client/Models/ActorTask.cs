using System.Text.Json.Nodes;

namespace Apify.Client.Models;

/// <summary>
/// A pre-configured Actor run (an Actor task).
/// </summary>
/// <remarks>
/// Named <c>ActorTask</c> rather than <c>Task</c> to avoid colliding with
/// <see cref="System.Threading.Tasks.Task"/>; it corresponds to the reference client's task resource.
/// </remarks>
public sealed class ActorTask : ApifyResource
{
    /// <summary>Wraps a raw task object.</summary>
    /// <param name="data">The raw decoded resource object.</param>
    public ActorTask(JsonObject data)
        : base(data)
    {
    }

    /// <summary>The unique task ID.</summary>
    public string? Id => GetString("id");

    /// <summary>The ID of the Actor this task runs.</summary>
    public string? ActId => GetString("actId");

    /// <summary>The ID of the user who owns the task.</summary>
    public string? UserId => GetString("userId");

    /// <summary>The technical name of the task.</summary>
    public string? Name => GetString("name");

    /// <summary>The human-readable title shown in the UI.</summary>
    public string? Title => GetString("title");

    /// <summary>When the task was created (ISO-8601 string).</summary>
    public string? CreatedAt => GetString("createdAt");

    /// <summary>When the task was last modified (ISO-8601 string).</summary>
    public string? ModifiedAt => GetString("modifiedAt");

    /// <summary>
    /// Whether the task is published on its public landing page in Apify Store. Derived from
    /// <see cref="PublicConfig"/>'s <c>publishedAt</c>; set it via <see cref="Resources.TaskClient.PublishAsync"/>
    /// or <see cref="Resources.TaskClient.UnpublishAsync"/>, not by writing this field directly.
    /// </summary>
    public bool? IsPublic => GetBool("isPublic");

    /// <summary>The task's public landing page display configuration, or <c>null</c> if not set.</summary>
    public TaskPublicConfig? PublicConfig => Get("publicConfig") is JsonObject obj ? new TaskPublicConfig(obj) : null;
}
