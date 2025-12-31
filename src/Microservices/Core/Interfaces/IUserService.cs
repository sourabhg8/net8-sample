using Microservices.Core.DTOs;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Service interface for User business operations
/// </summary>
public interface IUserService
{
    Task<UserResponse> GetByIdAsync(string id, string? requesterOrgId, bool isPlatformAdmin, CancellationToken cancellationToken = default);
    Task<UserListResponse> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<UserListResponse> GetByOrgIdAsync(string orgId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<UserResponse> CreateAsync(CreateUserRequest request, string orgId, string orgName, string createdBy, CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateAsync(string id, UpdateUserRequest request, string? requesterOrgId, bool isPlatformAdmin, string modifiedBy, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, string? requesterOrgId, bool isPlatformAdmin, string deletedBy, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(string userId, string? requesterOrgId, bool isPlatformAdmin, string resetBy, CancellationToken cancellationToken = default);
}

