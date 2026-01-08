// Error Handling Middleware - Fixed for record init property
using System.Net;
using System.Text.Json;

namespace LittleHelperAI.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        
        string detail;
        int statusCode;

        switch (exception)
        {
            case UnauthorizedAccessException:
                statusCode = (int)HttpStatusCode.Unauthorized;
                detail = exception.Message;
                break;
            case ArgumentException:
            case InvalidOperationException:
                statusCode = (int)HttpStatusCode.BadRequest;
                detail = exception.Message;
                break;
            case KeyNotFoundException:
                statusCode = (int)HttpStatusCode.NotFound;
                detail = exception.Message;
                break;
            default:
                statusCode = (int)HttpStatusCode.InternalServerError;
                detail = "An internal error occurred";
                break;
        }

        context.Response.StatusCode = statusCode;
        
        var response = new ErrorResponse(detail);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

public record ErrorResponse(string Detail);
