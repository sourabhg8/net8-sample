using Microservices.Core.DTOs;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Service interface for Organization business operations
/// </summary>
public interface IOrganizationService
{
    Task<OrganizationResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<OrganizationListResponse> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);
    Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, string createdBy, CancellationToken cancellationToken = default);
    Task<OrganizationResponse> UpdateAsync(string id, UpdateOrganizationRequest request, string modifiedBy, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken cancellationToken = default);
}

