namespace Microservices.Core.Exceptions;

/// <summary>
/// Exception thrown when a business rule is violated
/// </summary>
public class BusinessException : BaseException
{
    public BusinessException(string message) 
        : base(message, StatusCodes.Status422UnprocessableEntity, "BUSINESS_ERROR")
    {
    }

    public BusinessException(string message, string errorCode) 
        : base(message, StatusCodes.Status422UnprocessableEntity, errorCode)
    {
    }
}
