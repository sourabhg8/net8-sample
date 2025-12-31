using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Repositories;

/// <summary>
/// In-memory organization repository implementation (for demo purposes)
/// Will be replaced with Cosmos DB implementation
/// </summary>
public class OrganizationRepository : IOrganizationRepository
{
    private static readonly List<Organization> _organizations = new()
    {
        new Organization
        {
            Id = "org_01HABC",
            OrgId = "org_01HABC",
            Name = "Acme Corp",
            Status = "active",
            IsDeleted = false,
            Contact = new OrganizationContact
            {
                Email = "ops@acme.com",
                Phone = new PhoneNumber
                {
                    CountryCode = "+91",
                    Number = "9876543210",
                    E164 = "+919876543210"
                },
                Address = new Address
                {
                    Line1 = "12 MG Road",
                    Line2 = "Near Metro Station",
                    City = "Bengaluru",
                    State = "Karnataka",
                    PostalCode = "560001",
                    Country = "IN"
                }
            },
            Subscription = new OrganizationSubscription
            {
                Limits = new SubscriptionLimits { UserLimit = 25 }
            },
            CreatedAt = new DateTime(2025, 12, 24, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "platform_admin_001",
            ModifiedAt = new DateTime(2025, 12, 24, 0, 0, 0, DateTimeKind.Utc),
            ModifiedBy = "platform_admin_001",
            Version = 1
        },
        new Organization
        {
            Id = "org_02XYZD",
            OrgId = "org_02XYZD",
            Name = "TechStart Solutions",
            Status = "active",
            IsDeleted = false,
            Contact = new OrganizationContact
            {
                Email = "admin@techstart.io",
                Phone = new PhoneNumber
                {
                    CountryCode = "+1",
                    Number = "4155551234",
                    E164 = "+14155551234"
                },
                Address = new Address
                {
                    Line1 = "500 Startup Lane",
                    Line2 = "Suite 200",
                    City = "San Francisco",
                    State = "California",
                    PostalCode = "94107",
                    Country = "US"
                }
            },
            Subscription = new OrganizationSubscription
            {
                Limits = new SubscriptionLimits { UserLimit = 50 }
            },
            CreatedAt = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "platform_admin_001",
            ModifiedAt = new DateTime(2025, 11, 15, 0, 0, 0, DateTimeKind.Utc),
            ModifiedBy = "platform_admin_001",
            Version = 1
        },
        new Organization
        {
            Id = "org_03MNOP",
            OrgId = "org_03MNOP",
            Name = "Global Enterprises Ltd",
            Status = "suspended",
            IsDeleted = false,
            Contact = new OrganizationContact
            {
                Email = "contact@globalent.co.uk",
                Phone = new PhoneNumber
                {
                    CountryCode = "+44",
                    Number = "2079460123",
                    E164 = "+442079460123"
                },
                Address = new Address
                {
                    Line1 = "100 High Street",
                    Line2 = null,
                    City = "London",
                    State = "Greater London",
                    PostalCode = "EC1A 1BB",
                    Country = "GB"
                }
            },
            Subscription = new OrganizationSubscription
            {
                Limits = new SubscriptionLimits { UserLimit = 100 }
            },
            CreatedAt = new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = "platform_admin_002",
            ModifiedAt = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedBy = "platform_admin_001",
            Version = 3
        }
    };

    private readonly ILogger<OrganizationRepository> _logger;

    public OrganizationRepository(ILogger<OrganizationRepository> logger)
    {
        _logger = logger;
    }

    public Task<Organization?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting organization by ID: {OrgId}", id);
        var org = _organizations.FirstOrDefault(o => o.Id == id && !o.IsDeleted);
        return Task.FromResult(org);
    }

    public Task<IEnumerable<Organization>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all organizations - Page: {Page}, PageSize: {PageSize}", page, pageSize);
        var orgs = _organizations
            .Where(o => !o.IsDeleted)
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult<IEnumerable<Organization>>(orgs);
    }

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        var count = _organizations.Count(o => !o.IsDeleted);
        return Task.FromResult(count);
    }

    public Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating organization: {OrgName}", organization.Name);
        
        // Generate unique ID
        var uniqueId = $"org_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        organization.Id = uniqueId;
        organization.OrgId = uniqueId;
        organization.CreatedAt = DateTime.UtcNow;
        organization.ModifiedAt = DateTime.UtcNow;
        organization.Version = 1;
        
        _organizations.Add(organization);
        return Task.FromResult(organization);
    }

    public Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating organization: {OrgId}", organization.Id);
        
        var existingOrg = _organizations.FirstOrDefault(o => o.Id == organization.Id);
        if (existingOrg != null)
        {
            var index = _organizations.IndexOf(existingOrg);
            organization.ModifiedAt = DateTime.UtcNow;
            organization.Version = existingOrg.Version + 1;
            _organizations[index] = organization;
        }
        
        return Task.FromResult(organization);
    }

    public Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Soft deleting organization: {OrgId}", id);
        
        var org = _organizations.FirstOrDefault(o => o.Id == id);
        if (org != null)
        {
            org.IsDeleted = true;
            org.DeletedAt = DateTime.UtcNow;
            org.ModifiedBy = deletedBy;
            org.ModifiedAt = DateTime.UtcNow;
            org.Version++;
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }

    public Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var exists = _organizations.Any(o => 
            o.Name.Equals(name, StringComparison.OrdinalIgnoreCase) 
            && !o.IsDeleted
            && (excludeId == null || o.Id != excludeId));
        return Task.FromResult(exists);
    }
}

