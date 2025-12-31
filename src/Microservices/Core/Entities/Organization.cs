using System.Text.Json.Serialization;

namespace Microservices.Core.Entities;

/// <summary>
/// Represents an organization entity in the system
/// </summary>
public class Organization
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "Organization";

    [JsonPropertyName("orgId")]
    public string OrgId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active"; // active | suspended | cancelled

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; } = false;

    [JsonPropertyName("deletedAt")]
    public DateTime? DeletedAt { get; set; }

    [JsonPropertyName("contact")]
    public OrganizationContact Contact { get; set; } = new();

    [JsonPropertyName("subscription")]
    public OrganizationSubscription Subscription { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [JsonPropertyName("modifiedAt")]
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("modifiedBy")]
    public string ModifiedBy { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;
}

public class OrganizationContact
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("phone")]
    public PhoneNumber Phone { get; set; } = new();

    [JsonPropertyName("address")]
    public Address Address { get; set; } = new();
}

public class PhoneNumber
{
    [JsonPropertyName("countryCode")]
    public string CountryCode { get; set; } = string.Empty;

    [JsonPropertyName("number")]
    public string Number { get; set; } = string.Empty;

    [JsonPropertyName("e164")]
    public string E164 { get; set; } = string.Empty;
}

public class Address
{
    [JsonPropertyName("line1")]
    public string Line1 { get; set; } = string.Empty;

    [JsonPropertyName("line2")]
    public string? Line2 { get; set; }

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("postalCode")]
    public string PostalCode { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;
}

public class OrganizationSubscription
{
    [JsonPropertyName("limits")]
    public SubscriptionLimits Limits { get; set; } = new();
}

public class SubscriptionLimits
{
    [JsonPropertyName("userLimit")]
    public int UserLimit { get; set; } = 5;
}
