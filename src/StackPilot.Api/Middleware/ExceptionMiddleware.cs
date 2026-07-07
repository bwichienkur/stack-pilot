using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StackPilot.Application.Common;

namespace StackPilot.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        var (status, code, message) = ex switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "UNAUTHORIZED", ex.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, "NOT_FOUND", ex.Message),
            InvalidOperationException => (HttpStatusCode.BadRequest, "INVALID_OPERATION", ex.Message),
            ArgumentException => (HttpStatusCode.BadRequest, "VALIDATION_ERROR", ex.Message),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR",
                _env.IsDevelopment() ? ex.Message : "An unexpected error occurred")
        };

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json";

        var body = ApiResponse<object>.Fail(new ApiError { Code = code, Message = message });
        await context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}

public static class ExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionMiddleware>();
}
