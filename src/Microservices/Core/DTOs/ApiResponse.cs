using System.Text.Json.Serialization;

namespace Microservices.Core.DTOs;

/// <summary>
/// Standard API response wrapper
/// </summary>
/// <typeparam name="T">Type of data in response</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IEnumerable<string>? Errors { get; set; }

    public static ApiResponse<T> SuccessResponse(T data, string message = "Success", string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            CorrelationId = correlationId
        };
    }

    public static ApiResponse<T> FailureResponse(string message, IEnumerable<string>? errors = null, string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors,
            CorrelationId = correlationId
        };
    }
}

/// <summary>
/// Non-generic API response for operations without data
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    public new static ApiResponse SuccessResponse(string message = "Success", string? correlationId = null)
    {
        return new ApiResponse
        {
            Success = true,
            Message = message,
            CorrelationId = correlationId
        };
    }

    public new static ApiResponse FailureResponse(string message, IEnumerable<string>? errors = null, string? correlationId = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message,
            Errors = errors,
            CorrelationId = correlationId
        };
    }
}
