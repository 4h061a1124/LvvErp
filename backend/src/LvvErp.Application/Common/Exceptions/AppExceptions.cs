namespace LvvErp.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 404 by the API's exception handler.</summary>
public sealed class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For<T>(object key) => new($"{typeof(T).Name} '{key}' was not found.");
}

/// <summary>
/// Mapped to HTTP 409, e.g. a duplicate unique code. <see cref="ErrorCode"/> (see <see cref="ErrorCodes"/>)
/// is returned as the <c>errorCode</c> extension so the frontend can show a translated message.
/// </summary>
public sealed class ConflictException(string message, string? errorCode = null, object? details = null) : Exception(message)
{
    public string? ErrorCode { get; } = errorCode;

    /// <summary>Optional machine-readable payload, returned as the <c>details</c> extension.</summary>
    public object? Details { get; } = details;
}

/// <summary>Mapped to HTTP 401, e.g. wrong credentials.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);

/// <summary>Mapped to HTTP 403 for business-level permission checks.</summary>
public sealed class ForbiddenException(string message = "You do not have permission to perform this action.") : Exception(message);

/// <summary>Stable codes sent with 409 responses (ProblemDetails extension <c>errorCode</c>).</summary>
public static class ErrorCodes
{
    public const string DuplicateCode = "DuplicateCode";
    public const string DuplicateName = "DuplicateName";
    /// <summary>A delete was blocked because other records reference the row.</summary>
    public const string InUse = "InUse";
    public const string DocumentNotDraft = "DocumentNotDraft";
    public const string DocumentNotApproved = "DocumentNotApproved";
    /// <summary>Details: list of { lineNo, materialCode, lotNumber, requested, available }.</summary>
    public const string InsufficientStock = "InsufficientStock";
    public const string TrackLotsLocked = "TrackLotsLocked";
}
