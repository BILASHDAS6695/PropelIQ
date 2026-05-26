using Serilog.Context;

namespace HealthPlatform.Api.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the inbound request header, or generates a new
/// GUID if the header is absent. Stores the value in:
/// - HttpContext.Items["CorrelationId"] for downstream middleware/controllers
/// - HttpContext.TraceIdentifier (used by ASP.NET diagnostics)
/// - Serilog LogContext so every log event for this request includes CorrelationId
/// Echoes the value in the X-Correlation-Id response header.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("D");

        context.Items["CorrelationId"] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
