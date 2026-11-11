using System.Text.Json.Nodes;

namespace Apify.Client.Exceptions;

// Each subclass matches one HTTP status group and adds no behavior; construction and message formatting stay in
// ApifyApiException. Callers can catch the error kind (`catch (NotFoundException)`) instead of comparing the status.
// ApifyApiException.Create picks the subclass.

/// <summary>Thrown for a <c>400 Bad Request</c> response: the request was malformed or failed validation.</summary>
public sealed class InvalidRequestException : ApifyApiException
{
    /// <inheritdoc cref="ApifyApiException(int, string?, string, int, string, string, JsonObject?)" />
    public InvalidRequestException(int statusCode, string? type, string message, int attempt, string httpMethod, string path, JsonObject? data = null)
        : base(statusCode, type, message, attempt, httpMethod, path, data)
    {
    }
}

/// <summary>Thrown for a <c>401 Unauthorized</c> response: the API token is missing or invalid.</summary>
public sealed class UnauthorizedException : ApifyApiException
{
    /// <inheritdoc cref="ApifyApiException(int, string?, string, int, string, string, JsonObject?)" />
    public UnauthorizedException(int statusCode, string? type, string message, int attempt, string httpMethod, string path, JsonObject? data = null)
        : base(statusCode, type, message, attempt, httpMethod, path, data)
    {
    }
}

/// <summary>Thrown for a <c>403 Forbidden</c> response: the token is not permitted to perform the request.</summary>
public sealed class ForbiddenException : ApifyApiException
{
    /// <inheritdoc cref="ApifyApiException(int, string?, string, int, string, string, JsonObject?)" />
    public ForbiddenException(int statusCode, string? type, string message, int attempt, string httpMethod, string path, JsonObject? data = null)
        : base(statusCode, type, message, attempt, httpMethod, path, data)
    {
    }
}

/// <summary>
/// Thrown for a <c>404 Not Found</c> response that is not resolved to a missing resource. Clients addressed by ID
/// resolve an ordinary 404 to <c>null</c>. This type surfaces for clients without their own ID (a run's default
/// storages and log, a build's log) and for sub-path endpoints whose parent resource must exist.
/// </summary>
public sealed class NotFoundException : ApifyApiException
{
    /// <inheritdoc cref="ApifyApiException(int, string?, string, int, string, string, JsonObject?)" />
    public NotFoundException(int statusCode, string? type, string message, int attempt, string httpMethod, string path, JsonObject? data = null)
        : base(statusCode, type, message, attempt, httpMethod, path, data)
    {
    }
}

/// <summary>Thrown for a <c>409 Conflict</c> response: the request conflicts with the resource's current state.</summary>
public sealed class ConflictException : ApifyApiException
{
    /// <inheritdoc cref="ApifyApiException(int, string?, string, int, string, string, JsonObject?)" />
    public ConflictException(int statusCode, string? type, string message, int attempt, string httpMethod, string path, JsonObject? data = null)
        : base(statusCode, type, message, attempt, httpMethod, path, data)
    {
    }
}

/// <summary>
/// Thrown for a <c>429 Too Many Requests</c> response once the client's retries (see
/// <see cref="ApifyApiException.Attempt"/>) are spent.
/// </summary>
public sealed class RateLimitException : ApifyApiException
{
    /// <inheritdoc cref="ApifyApiException(int, string?, string, int, string, string, JsonObject?)" />
    public RateLimitException(int statusCode, string? type, string message, int attempt, string httpMethod, string path, JsonObject? data = null)
        : base(statusCode, type, message, attempt, httpMethod, path, data)
    {
    }
}

/// <summary>
/// Thrown for a <c>5xx</c> server-error response that exhausted the client's retries (see
/// <see cref="ApifyApiException.Attempt"/>).
/// </summary>
public sealed class ServerException : ApifyApiException
{
    /// <inheritdoc cref="ApifyApiException(int, string?, string, int, string, string, JsonObject?)" />
    public ServerException(int statusCode, string? type, string message, int attempt, string httpMethod, string path, JsonObject? data = null)
        : base(statusCode, type, message, attempt, httpMethod, path, data)
    {
    }
}
