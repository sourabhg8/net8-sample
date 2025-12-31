using System.ComponentModel.DataAnnotations;
using Microservices.Core.Entities;

namespace Microservices.Core.DTOs;

#region Request DTOs

/// <summary>
/// DTO for creating a new organization
/// </summary>
public class CreateOrganizationRequest
{
    [Required]
    [StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public ContactRequest Contact { get; set; } = new();

    public SubscriptionRequest? Subscription { get; set; }
}

/// <summary>
/// DTO for updating an existing organization
/// </summary>
public class UpdateOrganizationRequest
{
    [StringLength(200, MinimumLength = 2)]
    public string? Name { get; set; }

    public string? Status { get; set; } // active | suspended | cancelled

    public ContactRequest? Contact { get; set; }

    public SubscriptionRequest? Subscription { get; set; }
}

public class ContactRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public PhoneRequest? Phone { get; set; }

    public AddressRequest? Address { get; set; }
}

public class PhoneRequest
{
    [Required]
    public string CountryCode { get; set; } = string.Empty;

    [Required]
    public string Number { get; set; } = string.Empty;
}

public class AddressRequest
{
    [Required]
    public string Line1 { get; set; } = string.Empty;

    public string? Line2 { get; set; }

    [Required]
    public string City { get; set; } = string.Empty;

    [Required]
    public string State { get; set; } = string.Empty;

    [Required]
    public string PostalCode { get; set; } = string.Empty;

    [Required]
    public string Country { get; set; } = string.Empty;
}

public class SubscriptionRequest
{
    public int UserLimit { get; set; } = 5;
}

#endregion

#region Response DTOs

/// <summary>
/// DTO for organization response
/// </summary>
public class OrganizationResponse
{
    public string Id { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public ContactResponse Contact { get; set; } = new();
    public SubscriptionResponse Subscription { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public int Version { get; set; }

    public static OrganizationResponse FromEntity(Organization org)
    {
        return new OrganizationResponse
        {
            Id = org.Id,
            OrgId = org.OrgId,
            Name = org.Name,
            Status = org.Status,
            Contact = new ContactResponse
            {
                Email = org.Contact.Email,
                Phone = new PhoneResponse
                {
                    CountryCode = org.Contact.Phone.CountryCode,
                    Number = org.Contact.Phone.Number,
                    E164 = org.Contact.Phone.E164
                },
                Address = new AddressResponse
                {
                    Line1 = org.Contact.Address.Line1,
                    Line2 = org.Contact.Address.Line2,
                    City = org.Contact.Address.City,
                    State = org.Contact.Address.State,
                    PostalCode = org.Contact.Address.PostalCode,
                    Country = org.Contact.Address.Country
                }
            },
            Subscription = new SubscriptionResponse
            {
                Limits = new LimitsResponse
                {
                    UserLimit = org.Subscription.Limits.UserLimit
                }
            },
            CreatedAt = org.CreatedAt,
            CreatedBy = org.CreatedBy,
            ModifiedAt = org.ModifiedAt,
            ModifiedBy = org.ModifiedBy,
            Version = org.Version
        };
    }
}

public class ContactResponse
{
    public string Email { get; set; } = string.Empty;
    public PhoneResponse Phone { get; set; } = new();
    public AddressResponse Address { get; set; } = new();
}

public class PhoneResponse
{
    public string CountryCode { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string E164 { get; set; } = string.Empty;
}

public class AddressResponse
{
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class SubscriptionResponse
{
    public LimitsResponse Limits { get; set; } = new();
}

public class LimitsResponse
{
    public int UserLimit { get; set; }
}

/// <summary>
/// Paginated list response for organizations
/// </summary>
public class OrganizationListResponse
{
    public IEnumerable<OrganizationResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore => (Page * PageSize) < TotalCount;
}

#endregion

