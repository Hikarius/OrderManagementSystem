using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing request");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var status = (int)HttpStatusCode.InternalServerError;
        var title = "An unexpected error occurred.";

        context.Response.ContentType = "application/problem+json";

        if (exception is ValidationException valEx)
        {
            status = StatusCodes.Status400BadRequest;
            title = "One or more validation errors occurred.";
            var errors = valEx.Errors.Select(e => new { property = e.PropertyName, message = e.ErrorMessage }).ToArray();

            var pd = new
            {
                type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                title,
                status,
                detail = "See the errors property for details.",
                instance = context.Request.Path,
                errors
            };

            context.Response.StatusCode = status;
            await context.Response.WriteAsync(JsonSerializer.Serialize(pd, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            return;
        }

        var problem = new
        {
            type = "about:blank",
            title,
            status,
            detail = exception.Message,
            instance = context.Request.Path
        };

        context.Response.StatusCode = status;
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
