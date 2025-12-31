using Microservices.Core.DTOs;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Service interface for authentication operations
/// </summary>
public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<bool> ValidateUserAsync(string username, string password, CancellationToken cancellationToken = default);
}
