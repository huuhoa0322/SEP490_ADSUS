using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Exceptions;
using FluentValidation;

namespace ADSUS_BE.Middlewares;

/// <summary>
/// Catches every unhandled exception so Controllers/Services never wrap calls in a local
/// try/catch just to format an error response (L3 §10, L2 §9). Registered as the very first
/// middleware in Program.cs so it wraps everything downstream (CORS, auth, controllers).
/// </summary>
public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // UnsafeRelaxedJsonEscaping đảm bảo tiếng Việt encode đúng UTF-8, không fallback.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
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
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            ResourceNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            ConflictException => (HttpStatusCode.Conflict, exception.Message),
            BusinessException => (HttpStatusCode.UnprocessableEntity, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, exception.Message),
            ValidationException validationException => (
                HttpStatusCode.BadRequest,
                string.Join("; ", validationException.Errors.Select(e => e.ErrorMessage))),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again later."),
        };

        // Full detail always goes to the log (Serilog sinks) — the message above is what the
        // client sees, and for a 500 it is deliberately generic; never leak the raw exception.
        // Method/Path come straight from the request, so strip CR/LF before logging them —
        // otherwise a path like "/x%0D%0AFake log line" could forge extra log entries.
        var method = SanitizeForLog(context.Request.Method);
        var path = SanitizeForLog(context.Request.Path.Value);

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception on {Method} {Path}", method, path);
        }
        else
        {
            _logger.LogWarning(exception, "{ExceptionType} on {Method} {Path}", exception.GetType().Name, method, path);
        }

        var response = ApiResponse<object?>.Fail((int)statusCode, message);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions), context.RequestAborted);
    }

    private static string SanitizeForLog(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\r', '_').Replace('\n', '_');
}
