using Microservices.Core.Entities;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Repository interface for User entity operations
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetByOrgIdAsync(string orgId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetTotalCountByOrgIdAsync(string orgId, CancellationToken cancellationToken = default);
    Task<User> CreateAsync(User user, CancellationToken cancellationToken = default);
    Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, string? excludeId = null, CancellationToken cancellationToken = default);
}
