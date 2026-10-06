using System.Text.Json.Nodes;

namespace Apify.Client.Exceptions;

// Each subclass below matches one HTTP status group and carries no behavior of its own: construction and
// message formatting stay in the base ApifyApiException. They let a caller branch on the error kind with a
// type check (`catch (NotFoundException)`) instead of comparing StatusCode/Type, matching the reference
// client's and the Python client's ApifyApiError subclass hierarchy. See ApifyApiException.Create, which
// picks the subclass for a given status code.

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
/// Thrown for a <c>404 Not Found</c> response that could not be resolved to a single, unambiguous missing
/// resource (see <see cref="ApifyApiException"/> remarks on the client's resources). Most resource clients
/// addressed by id resolve an ordinary 404 to <c>null</c> instead of throwing; this type still surfaces for
/// chained clients that have no id of their own (e.g. a run's default dataset/key-value
/// store/request queue/log, or a build's log) and for the sub-path endpoints that always require their
/// parent resource to exist.
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
/// Thrown for a <c>429 Too Many Requests</c> response that exhausted the client's retries. The client
/// already retries a rate-limited request with backoff (see <see cref="ApifyApiException.Attempt"/>); this
/// type reaches the caller only once that budget is spent.
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
