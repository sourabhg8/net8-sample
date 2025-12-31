using System.Text.Json;
using Microservices.Core.DTOs;
using Microservices.Core.Exceptions;

namespace Microservices.Middleware;

/// <summary>
/// Global exception handling middleware
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
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
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.GetCorrelationId();

        // Log the exception
        LogException(exception, correlationId);

        var (statusCode, response) = GetErrorResponse(exception, correlationId);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        };

        var json = JsonSerializer.Serialize(response, jsonOptions);
        await context.Response.WriteAsync(json);
    }

    private void LogException(Exception exception, string? correlationId)
    {
        var logMessage = "An exception occurred. CorrelationId: {CorrelationId}";

        switch (exception)
        {
            case ValidationException:
                _logger.LogWarning(exception, logMessage, correlationId);
                break;
            case UnauthorizedException:
                _logger.LogWarning(exception, logMessage, correlationId);
                break;
            case ForbiddenException:
                _logger.LogWarning(exception, logMessage, correlationId);
                break;
            case NotFoundException:
                _logger.LogWarning(exception, logMessage, correlationId);
                break;
            case BusinessException:
                _logger.LogWarning(exception, logMessage, correlationId);
                break;
            case ConflictException:
                _logger.LogWarning(exception, logMessage, correlationId);
                break;
            default:
                _logger.LogError(exception, logMessage, correlationId);
                break;
        }
    }

    private (int StatusCode, object Response) GetErrorResponse(Exception exception, string? correlationId)
    {
        return exception switch
        {
            ValidationException validationEx => (
                validationEx.StatusCode,
                new ErrorResponse
                {
                    Success = false,
                    Message = validationEx.Message,
                    ErrorCode = validationEx.ErrorCode,
                    Errors = validationEx.Errors,
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                }
            ),
            
            BaseException baseEx => (
                baseEx.StatusCode,
                new ErrorResponse
                {
                    Success = false,
                    Message = baseEx.Message,
                    ErrorCode = baseEx.ErrorCode,
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                }
            ),

            ArgumentException argEx => (
                StatusCodes.Status400BadRequest,
                new ErrorResponse
                {
                    Success = false,
                    Message = argEx.Message,
                    ErrorCode = "INVALID_ARGUMENT",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                }
            ),

            OperationCanceledException => (
                StatusCodes.Status499ClientClosedRequest,
                new ErrorResponse
                {
                    Success = false,
                    Message = "Request was cancelled",
                    ErrorCode = "REQUEST_CANCELLED",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow
                }
            ),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ErrorResponse
                {
                    Success = false,
                    Message = _environment.IsDevelopment() 
                        ? exception.Message 
                        : "An unexpected error occurred. Please try again later.",
                    ErrorCode = "INTERNAL_SERVER_ERROR",
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow,
                    Details = _environment.IsDevelopment() ? exception.StackTrace : null
                }
            )
        };
    }
}

/// <summary>
/// Error response model
/// </summary>
public class ErrorResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; }
    public IEnumerable<string>? Errors { get; set; }
    public string? Details { get; set; }
}

/// <summary>
/// Extension methods for ExceptionHandlingMiddleware
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
