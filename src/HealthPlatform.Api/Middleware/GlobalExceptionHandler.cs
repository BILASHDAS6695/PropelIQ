using FluentValidation;
using HealthPlatform.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Middleware;

/// <summary>
/// IExceptionHandler implementation registered with UseExceptionHandler().
/// Maps domain exceptions to RFC 7807 ProblemDetails responses.
/// Stack traces are included only in Development; never exposed in Production.
/// </summary>
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            NotFoundException           => (StatusCodes.Status404NotFound,            "Resource Not Found"),
            ConflictException           => (StatusCodes.Status409Conflict,            "Conflict"),
            ValidationException         => (StatusCodes.Status400BadRequest,          "Validation Failed"),
            ArgumentException           => (StatusCodes.Status400BadRequest,          "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized,        "Unauthorized"),
            _                           => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        _logger.LogError(
            exception,
            "Unhandled exception {ExceptionType}: {Message}",
            exception.GetType().Name,
            exception.Message);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions["errors"] = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray());
        }

        // Expose stack trace only in Development — never in Production
        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["stackTrace"] = exception.StackTrace;
        }

        if (httpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var correlationId))
        {
            problemDetails.Extensions["correlationId"] = (string?)correlationId;
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
