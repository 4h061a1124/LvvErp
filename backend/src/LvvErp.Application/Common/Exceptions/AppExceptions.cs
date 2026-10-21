namespace LvvErp.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 404 by the API's exception handler.</summary>
public sealed class NotFoundException(string message) : Exception(message)
{
    public static NotFoundException For<T>(object key) => new($"{typeof(T).Name} '{key}' was not found.");
}

/// <summary>Mapped to HTTP 409, e.g. a duplicate unique code.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Mapped to HTTP 401, e.g. wrong credentials.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);

/// <summary>Mapped to HTTP 403 for business-level permission checks.</summary>
public sealed class ForbiddenException(string message = "You do not have permission to perform this action.") : Exception(message);
