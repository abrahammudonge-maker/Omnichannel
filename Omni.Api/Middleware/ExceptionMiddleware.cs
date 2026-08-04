using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Omni.Shared.Responses;

namespace Omni.Api.Middleware;

public sealed class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);
            context.Response.StatusCode = ex is UnauthorizedAccessException
                ? (int)HttpStatusCode.Unauthorized
                : (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            var payload = ApiResponse<object>.Fail(ex.Message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
        }
    }
}
