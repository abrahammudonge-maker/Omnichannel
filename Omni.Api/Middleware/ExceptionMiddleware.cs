using System.Net;
using System.Text.Json;
using Omni.Shared.Responses;

namespace Omni.Api.Middleware;

public sealed class ExceptionMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // Exceptions we deliberately throw to communicate something to the caller (bad login, duplicate email, etc.) —
    // their message is always safe to show. Anything else is an unexpected failure whose details should only
    // surface in Development; in Production we log it and return a generic message.
    private static readonly Type[] UserFacingExceptionTypes = { typeof(UnauthorizedAccessException), typeof(InvalidOperationException) };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
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

            var isUserFacing = UserFacingExceptionTypes.Contains(ex.GetType());
            var message = isUserFacing || _environment.IsDevelopment()
                ? ex.Message
                : "An unexpected error occurred. Please try again or contact support.";

            var payload = ApiResponse<object>.Fail(message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload, SerializerOptions));
        }
    }
}
