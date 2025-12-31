namespace Microservices.Core.Exceptions;

/// <summary>
/// Exception thrown when request validation fails
/// </summary>
public class ValidationException : BaseException
{
    public IEnumerable<string> Errors { get; }

    public ValidationException(string message) 
        : base(message, StatusCodes.Status400BadRequest, "VALIDATION_ERROR")
    {
        Errors = new[] { message };
    }

    public ValidationException(IEnumerable<string> errors) 
        : base("One or more validation errors occurred.", StatusCodes.Status400BadRequest, "VALIDATION_ERROR")
    {
        Errors = errors;
    }
}
