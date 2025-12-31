using Microservices.Core.DTOs;
using Microservices.Core.Entities;
using Microservices.Core.Exceptions;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

/// <summary>
/// Organization service implementation
/// </summary>
public class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<OrganizationService> _logger;

    private static readonly string[] ValidStatuses = { "active", "suspended", "cancelled" };

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        ILogger<OrganizationService> logger)
    {
        _organizationRepository = organizationRepository;
        _logger = logger;
    }

    public async Task<OrganizationResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting organization by ID: {OrgId}", id);

        var organization = await _organizationRepository.GetByIdAsync(id, cancellationToken);

        if (organization == null)
        {
            _logger.LogWarning("Organization not found: {OrgId}", id);
            throw new NotFoundException("Organization", id);
        }

        return OrganizationResponse.FromEntity(organization);
    }

    public async Task<OrganizationListResponse> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting all organizations - Page: {Page}, PageSize: {PageSize}", page, pageSize);

        var organizations = await _organizationRepository.GetAllAsync(page, pageSize, cancellationToken);
        var totalCount = await _organizationRepository.GetTotalCountAsync(cancellationToken);

        return new OrganizationListResponse
        {
            Items = organizations.Select(OrganizationResponse.FromEntity),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, string createdBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating organization: {OrgName} by {CreatedBy}", request.Name, createdBy);

        // Check if organization with same name already exists
        var exists = await _organizationRepository.ExistsByNameAsync(request.Name, cancellationToken: cancellationToken);
        if (exists)
        {
            _logger.LogWarning("Organization with name already exists: {OrgName}", request.Name);
            throw new ConflictException("Organization", $"An organization with name '{request.Name}' already exists");
        }

        var organization = new Organization
        {
            Name = request.Name,
            Status = "active",
            CreatedBy = createdBy,
            ModifiedBy = createdBy,
            Contact = new OrganizationContact
            {
                Email = request.Contact.Email,
                Phone = request.Contact.Phone != null ? new PhoneNumber
                {
                    CountryCode = request.Contact.Phone.CountryCode,
                    Number = request.Contact.Phone.Number,
                    E164 = $"{request.Contact.Phone.CountryCode}{request.Contact.Phone.Number}"
                } : new PhoneNumber(),
                Address = request.Contact.Address != null ? new Address
                {
                    Line1 = request.Contact.Address.Line1,
                    Line2 = request.Contact.Address.Line2,
                    City = request.Contact.Address.City,
                    State = request.Contact.Address.State,
                    PostalCode = request.Contact.Address.PostalCode,
                    Country = request.Contact.Address.Country
                } : new Address()
            },
            Subscription = request.Subscription != null ? new OrganizationSubscription
            {
                Limits = new SubscriptionLimits { UserLimit = request.Subscription.UserLimit }
            } : new OrganizationSubscription
            {
                Limits = new SubscriptionLimits { UserLimit = 5 }
            }
        };

        var created = await _organizationRepository.CreateAsync(organization, cancellationToken);
        
        _logger.LogInformation("Organization created successfully: {OrgId}", created.Id);

        return OrganizationResponse.FromEntity(created);
    }

    public async Task<OrganizationResponse> UpdateAsync(string id, UpdateOrganizationRequest request, string modifiedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating organization: {OrgId} by {ModifiedBy}", id, modifiedBy);

        var organization = await _organizationRepository.GetByIdAsync(id, cancellationToken);

        if (organization == null)
        {
            _logger.LogWarning("Organization not found for update: {OrgId}", id);
            throw new NotFoundException("Organization", id);
        }

        // Validate status if provided
        if (!string.IsNullOrEmpty(request.Status))
        {
            if (!ValidStatuses.Contains(request.Status.ToLower()))
            {
                throw new ValidationException(new[] { $"Invalid status. Valid values are: {string.Join(", ", ValidStatuses)}" });
            }
            organization.Status = request.Status.ToLower();
        }

        // Check for name uniqueness if name is being changed
        if (!string.IsNullOrEmpty(request.Name) && !request.Name.Equals(organization.Name, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _organizationRepository.ExistsByNameAsync(request.Name, id, cancellationToken);
            if (exists)
            {
                throw new ConflictException("Organization", $"An organization with name '{request.Name}' already exists");
            }
            organization.Name = request.Name;
        }

        // Update contact if provided
        if (request.Contact != null)
        {
            organization.Contact.Email = request.Contact.Email;
            
            if (request.Contact.Phone != null)
            {
                organization.Contact.Phone = new PhoneNumber
                {
                    CountryCode = request.Contact.Phone.CountryCode,
                    Number = request.Contact.Phone.Number,
                    E164 = $"{request.Contact.Phone.CountryCode}{request.Contact.Phone.Number}"
                };
            }

            if (request.Contact.Address != null)
            {
                organization.Contact.Address = new Address
                {
                    Line1 = request.Contact.Address.Line1,
                    Line2 = request.Contact.Address.Line2,
                    City = request.Contact.Address.City,
                    State = request.Contact.Address.State,
                    PostalCode = request.Contact.Address.PostalCode,
                    Country = request.Contact.Address.Country
                };
            }
        }

        // Update subscription if provided
        if (request.Subscription != null)
        {
            organization.Subscription = new OrganizationSubscription
            {
                Limits = new SubscriptionLimits { UserLimit = request.Subscription.UserLimit }
            };
        }

        organization.ModifiedBy = modifiedBy;

        var updated = await _organizationRepository.UpdateAsync(organization, cancellationToken);

        _logger.LogInformation("Organization updated successfully: {OrgId}, Version: {Version}", updated.Id, updated.Version);

        return OrganizationResponse.FromEntity(updated);
    }

    public async Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting organization: {OrgId} by {DeletedBy}", id, deletedBy);

        var organization = await _organizationRepository.GetByIdAsync(id, cancellationToken);

        if (organization == null)
        {
            _logger.LogWarning("Organization not found for deletion: {OrgId}", id);
            throw new NotFoundException("Organization", id);
        }

        var result = await _organizationRepository.DeleteAsync(id, deletedBy, cancellationToken);

        _logger.LogInformation("Organization deleted successfully: {OrgId}", id);

        return result;
    }
}

