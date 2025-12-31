using System.ComponentModel.DataAnnotations;

namespace Microservices.Core.DTOs;

#region Request DTOs

/// <summary>
/// DTO for creating a new user
/// Password is auto-generated: first 4 letters of email + "_" + first 4 letters of name
/// </summary>
public class CreateUserRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string UserType { get; set; } = "org_user"; // org_user | org_admin

    public string? Role { get; set; } // Optional: defaults to UserType if not provided
}

/// <summary>
/// DTO for updating an existing user (password cannot be changed here)
/// </summary>
public class UpdateUserRequest
{
    [StringLength(100, MinimumLength = 2)]
    public string? Name { get; set; }

    [EmailAddress]
    public string? Email { get; set; }

    public string? UserType { get; set; }

    public string? Role { get; set; }

    public string? Status { get; set; } // active | suspended
}

/// <summary>
/// DTO for changing password (authenticated user only)
/// </summary>
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

#endregion

#region Response DTOs

/// <summary>
/// DTO for user response
/// </summary>
public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public string OrgName { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public string ModifiedBy { get; set; } = string.Empty;
    public int Version { get; set; }

    public static UserResponse FromEntity(Microservices.Core.Entities.User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            UserId = user.UserId,
            OrgId = user.OrgId,
            OrgName = user.OrgName,
            UserType = user.UserType,
            Role = user.Role,
            Status = user.Status,
            Name = user.Name,
            Email = user.Email,
            CreatedAt = user.CreatedAt,
            CreatedBy = user.CreatedBy,
            ModifiedAt = user.ModifiedAt,
            ModifiedBy = user.ModifiedBy,
            Version = user.Version
        };
    }
}

/// <summary>
/// Paginated list response for users
/// </summary>
public class UserListResponse
{
    public IEnumerable<UserResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore => (Page * PageSize) < TotalCount;
}

#endregion

