using IntegrationHub.Application;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace IntegrationHub.Api;

public sealed class ApiExceptionHandler(IProblemDetailsService problems, ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (404, "Resource not found"),
            ConflictException or Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (409, "Definition conflict"),
            Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Microsoft.Data.SqlClient.SqlException { Number: 547 or 2601 or 2627 } } => (409, "A referenced record or unique identity changed"),
            Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Microsoft.Data.Sqlite.SqliteException { SqliteErrorCode: 19 } } => (409, "A referenced record or unique identity changed"),
            Microsoft.EntityFrameworkCore.DbUpdateException { InnerException: Microsoft.Data.Sqlite.SqliteException } or Microsoft.Data.Sqlite.SqliteException => (503, "Catalogue database unavailable"),
            DefinitionValidationException => (422, "Definition validation failed"),
            ArgumentException or BadHttpRequestException or System.Text.Json.JsonException => (400, "Invalid request"),
            Microsoft.Data.SqlClient.SqlException => (503, "Catalogue database unavailable"),
            _ => (500, "An unexpected error occurred")
        };
        // Never include request bodies, definitions, SQL text or connection strings in logs.
        logger.Log(status >= 500 ? LogLevel.Error : LogLevel.Information, "Request {Method} {Path} failed with {Status}; exception type {ExceptionType}; trace {TraceId}", context.Request.Method, context.Request.Path, status, exception.GetType().Name, context.TraceIdentifier);
        context.Response.StatusCode = status;
        var detail = new ProblemDetails { Status = status, Title = title, Detail = status < 500 && exception is not Microsoft.EntityFrameworkCore.DbUpdateException ? exception.Message : "The request could not be completed. Check service readiness or contact support with the trace ID.", Instance = context.Request.Path };
        detail.Extensions["traceId"] = context.TraceIdentifier;
        if (exception is DefinitionValidationException validation) detail.Extensions["validation"] = validation.Validation;
        return await problems.TryWriteAsync(new() { HttpContext = context, ProblemDetails = detail });
    }
}
