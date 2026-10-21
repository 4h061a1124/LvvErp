using FluentValidation;
using LvvErp.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LvvErp.Api.Infrastructure;

/// <summary>Maps exceptions to RFC 7807 ProblemDetails responses.</summary>
public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException ex => new ValidationProblemDetails(
                ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            NotFoundException ex => new() { Status = StatusCodes.Status404NotFound, Title = "Not found", Detail = ex.Message },
            ConflictException ex => new() { Status = StatusCodes.Status409Conflict, Title = "Conflict", Detail = ex.Message },
            UnauthorizedException ex => new() { Status = StatusCodes.Status401Unauthorized, Title = "Unauthorized", Detail = ex.Message },
            ForbiddenException ex => new() { Status = StatusCodes.Status403Forbidden, Title = "Forbidden", Detail = ex.Message },
            // Unique index hit by a concurrent request that slipped past the service-level check.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => new()
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = "A record with the same unique value already exists.",
            },
            _ => new() { Status = StatusCodes.Status500InternalServerError, Title = "An unexpected error occurred." },
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new()
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
