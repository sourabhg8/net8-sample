using Microservices.Core.Entities;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Repository interface for Organization entity operations
/// </summary>
public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Organization>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default);
    Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default);
}

