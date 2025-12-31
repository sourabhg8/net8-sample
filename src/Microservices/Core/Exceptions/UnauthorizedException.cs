namespace Microservices.Core.Exceptions;

/// <summary>
/// Exception thrown when authentication fails
/// </summary>
public class UnauthorizedException : BaseException
{
    public UnauthorizedException(string message = "Invalid credentials") 
        : base(message, StatusCodes.Status401Unauthorized, "UNAUTHORIZED")
    {
    }
}
