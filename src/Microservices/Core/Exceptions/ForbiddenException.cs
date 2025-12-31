namespace Microservices.Core.Exceptions;

/// <summary>
/// Exception thrown when user doesn't have permission to access a resource
/// </summary>
public class ForbiddenException : BaseException
{
    public ForbiddenException(string message = "You do not have permission to access this resource") 
        : base(message, StatusCodes.Status403Forbidden, "FORBIDDEN")
    {
    }
}
