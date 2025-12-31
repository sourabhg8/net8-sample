using System.Text.Json.Serialization;

namespace Microservices.Core.Entities;

/// <summary>
/// Represents a user entity in the system (Cosmos DB schema)
/// </summary>
public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("orgId")]
    public string OrgId { get; set; } = string.Empty;

    [JsonPropertyName("orgName")]
    public string OrgName { get; set; } = string.Empty;

    [JsonPropertyName("userType")]
    public string UserType { get; set; } = "org_user"; // org_user | platform_admin | org_admin

    [JsonPropertyName("role")]
    public string Role { get; set; } = "org_user"; // org_user | platform_admin | org_admin (single role per user)

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active"; // active | suspended

    [JsonPropertyName("isDeleted")]
    public bool IsDeleted { get; set; } = false;

    [JsonPropertyName("deletedAt")]
    public DateTime? DeletedAt { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("auth")]
    public UserAuth Auth { get; set; } = new();

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

    // Helper property for backward compatibility (used in login)
    [JsonIgnore]
    public string Username => Email;

    [JsonIgnore]
    public string Password => Auth.PasswordHash;
}

public class UserAuth
{
    [JsonPropertyName("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;
}
