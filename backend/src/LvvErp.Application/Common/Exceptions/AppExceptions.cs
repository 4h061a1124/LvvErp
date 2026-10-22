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
    public const string DuplicateTaxCode = "DuplicateTaxCode";
    /// <summary>A delete was blocked because other records reference the row.</summary>
    public const string InUse = "InUse";
    public const string DocumentNotDraft = "DocumentNotDraft";
    public const string DocumentNotApproved = "DocumentNotApproved";
    /// <summary>Details: list of { lineNo, materialCode, lotNumber, requested, available }.</summary>
    public const string InsufficientStock = "InsufficientStock";
    public const string TrackLotsLocked = "TrackLotsLocked";
    /// <summary>The action (submit, approve, cancel, ...) is not allowed in the document's current status.</summary>
    public const string InvalidStatus = "InvalidStatus";
    /// <summary>The account has no linked employee, so no department to raise a request for.</summary>
    public const string NoDepartment = "NoDepartment";
    /// <summary>A purchase request on a (non-cancelled) order cannot be cancelled.</summary>
    public const string RequestHasOrders = "RequestHasOrders";
    /// <summary>Details: numbers of the unapproved orders still drawing from the request.</summary>
    public const string RequestHasOpenOrders = "RequestHasOpenOrders";
    /// <summary>Details: list of { lineNo, materialCode, ordered, received, receiving, allowed } per purchase order line.</summary>
    public const string OverReceipt = "OverReceipt";
    public const string DuplicateUserName = "DuplicateUserName";
    /// <summary>The employee is already linked to another account.</summary>
    public const string EmployeeAlreadyLinked = "EmployeeAlreadyLinked";
    /// <summary>An admin tried to deactivate their own account or remove their own Admin role.</summary>
    public const string CannotChangeOwnAccess = "CannotChangeOwnAccess";
}
