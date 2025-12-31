namespace Microservices.Core.Exceptions;

/// <summary>
/// Exception thrown when there's a conflict with the current state of the resource
/// </summary>
public class ConflictException : BaseException
{
    public ConflictException(string message) 
        : base(message, StatusCodes.Status409Conflict, "CONFLICT")
    {
    }

    public ConflictException(string resourceName, string conflictReason) 
        : base($"{resourceName}: {conflictReason}", StatusCodes.Status409Conflict, "CONFLICT")
    {
    }
}
