using Microservices.Core.Entities;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Service interface for JWT token operations
/// </summary>
public interface IJwtService
{
    string GenerateToken(User user);
    bool ValidateToken(string token);
}
