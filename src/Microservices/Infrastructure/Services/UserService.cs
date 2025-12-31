using Microservices.Core.DTOs;
using Microservices.Core.Entities;
using Microservices.Core.Exceptions;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

/// <summary>
/// User service implementation
/// </summary>
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<UserService> _logger;

    private static readonly string[] ValidStatuses = { "active", "suspended" };
    private static readonly string[] ValidUserTypes = { "org_user", "org_admin" };

    public UserService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task<UserResponse> GetByIdAsync(string id, string? requesterOrgId, bool isPlatformAdmin, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting user by ID: {UserId}", id);

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User not found: {UserId}", id);
            throw new NotFoundException("User", id);
        }

        // Check access: Platform admin can see all, org admin can only see users from their org
        if (!isPlatformAdmin && user.OrgId != requesterOrgId)
        {
            _logger.LogWarning("Access denied: User {RequesterId} tried to access user {UserId} from different org", requesterOrgId, id);
            throw new ForbiddenException("You can only view users from your organization");
        }

        return UserResponse.FromEntity(user);
    }

    public async Task<UserListResponse> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting all users - Page: {Page}, PageSize: {PageSize}", page, pageSize);

        var users = await _userRepository.GetAllAsync(page, pageSize, cancellationToken);
        var totalCount = await _userRepository.GetTotalCountAsync(cancellationToken);

        return new UserListResponse
        {
            Items = users.Select(UserResponse.FromEntity),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserListResponse> GetByOrgIdAsync(string orgId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting users by OrgId: {OrgId} - Page: {Page}, PageSize: {PageSize}", orgId, page, pageSize);

        var users = await _userRepository.GetByOrgIdAsync(orgId, page, pageSize, cancellationToken);
        var totalCount = await _userRepository.GetTotalCountByOrgIdAsync(orgId, cancellationToken);

        return new UserListResponse
        {
            Items = users.Select(UserResponse.FromEntity),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, string orgId, string orgName, string createdBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating user: {Email} for org: {OrgId} by {CreatedBy}", request.Email, orgId, createdBy);

        // Validate user type
        if (!ValidUserTypes.Contains(request.UserType.ToLower()))
        {
            throw new ValidationException(new[] { $"Invalid user type. Valid values are: {string.Join(", ", ValidUserTypes)}" });
        }

        // Check if email already exists
        var exists = await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken: cancellationToken);
        if (exists)
        {
            _logger.LogWarning("User with email already exists: {Email}", request.Email);
            throw new ConflictException("User", $"A user with email '{request.Email}' already exists");
        }

        // Auto-generate password: first 4 letters of email + "_" + first 4 letters of name
        var generatedPassword = GeneratePassword(request.Email, request.Name);
        
        // Hash the password before storing
        var hashedPassword = _passwordService.HashPassword(generatedPassword);

        var user = new User
        {
            OrgId = orgId,
            OrgName = orgName,
            UserType = request.UserType.ToLower(),
            Role = request.Role ?? request.UserType.ToLower(), // Default to UserType if Role not provided
            Status = "active",
            Name = request.Name,
            Email = request.Email,
            Auth = new UserAuth { PasswordHash = hashedPassword },
            CreatedBy = createdBy,
            ModifiedBy = createdBy
        };

        var created = await _userRepository.CreateAsync(user, cancellationToken);
        
        _logger.LogInformation("User created successfully: {UserId} with auto-generated password (hashed)", created.Id);

        return UserResponse.FromEntity(created);
    }

    /// <summary>
    /// Generate password: first 4 letters of email (before @) + "_" + first 4 letters of name
    /// </summary>
    private static string GeneratePassword(string email, string name)
    {
        var emailPart = email.Split('@')[0];
        var emailPrefix = emailPart.Length >= 4 ? emailPart[..4].ToLower() : emailPart.ToLower();
        
        var namePart = name.Replace(" ", "");
        var namePrefix = namePart.Length >= 4 ? namePart[..4].ToLower() : namePart.ToLower();
        
        return $"{emailPrefix}_{namePrefix}";
    }

    public async Task<UserResponse> UpdateAsync(string id, UpdateUserRequest request, string? requesterOrgId, bool isPlatformAdmin, string modifiedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating user: {UserId} by {ModifiedBy}", id, modifiedBy);

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User not found for update: {UserId}", id);
            throw new NotFoundException("User", id);
        }

        // Check access: Platform admin can update all, org admin can only update users from their org
        if (!isPlatformAdmin && user.OrgId != requesterOrgId)
        {
            _logger.LogWarning("Access denied: User tried to update user {UserId} from different org", id);
            throw new ForbiddenException("You can only update users from your organization");
        }

        // Validate status if provided
        if (!string.IsNullOrEmpty(request.Status))
        {
            if (!ValidStatuses.Contains(request.Status.ToLower()))
            {
                throw new ValidationException(new[] { $"Invalid status. Valid values are: {string.Join(", ", ValidStatuses)}" });
            }
            user.Status = request.Status.ToLower();
        }

        // Validate user type if provided
        if (!string.IsNullOrEmpty(request.UserType))
        {
            if (!ValidUserTypes.Contains(request.UserType.ToLower()))
            {
                throw new ValidationException(new[] { $"Invalid user type. Valid values are: {string.Join(", ", ValidUserTypes)}" });
            }
            user.UserType = request.UserType.ToLower();
        }

        // Update name if provided
        if (!string.IsNullOrEmpty(request.Name))
        {
            user.Name = request.Name;
        }

        // Update email if provided (check uniqueness)
        if (!string.IsNullOrEmpty(request.Email) && !request.Email.Equals(user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var exists = await _userRepository.ExistsByEmailAsync(request.Email, id, cancellationToken);
            if (exists)
            {
                throw new ConflictException("User", $"A user with email '{request.Email}' already exists");
            }
            user.Email = request.Email;
        }

        // Note: Password cannot be changed via update - use ChangePassword endpoint

        // Update role if provided
        if (!string.IsNullOrEmpty(request.Role))
        {
            user.Role = request.Role.ToLower();
        }

        user.ModifiedBy = modifiedBy;

        var updated = await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("User updated successfully: {UserId}, Version: {Version}", updated.Id, updated.Version);

        return UserResponse.FromEntity(updated);
    }

    public async Task<bool> DeleteAsync(string id, string? requesterOrgId, bool isPlatformAdmin, string deletedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting user: {UserId} by {DeletedBy}", id, deletedBy);

        var user = await _userRepository.GetByIdAsync(id, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User not found for deletion: {UserId}", id);
            throw new NotFoundException("User", id);
        }

        // Check access: Platform admin can delete all, org admin can only delete users from their org
        if (!isPlatformAdmin && user.OrgId != requesterOrgId)
        {
            _logger.LogWarning("Access denied: User tried to delete user {UserId} from different org", id);
            throw new ForbiddenException("You can only delete users from your organization");
        }

        // Prevent deleting platform admin
        if (user.UserType == "platform_admin")
        {
            throw new ForbiddenException("Cannot delete platform admin user");
        }

        var result = await _userRepository.DeleteAsync(id, deletedBy, cancellationToken);

        _logger.LogInformation("User deleted successfully: {UserId}", id);

        return result;
    }

    public async Task<bool> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Changing password for user: {UserId}", userId);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User not found for password change: {UserId}", userId);
            throw new NotFoundException("User", userId);
        }

        // Verify current password using password service
        if (!_passwordService.VerifyPassword(request.CurrentPassword, user.Auth.PasswordHash))
        {
            _logger.LogWarning("Invalid current password for user: {UserId}", userId);
            throw new UnauthorizedException("Current password is incorrect");
        }

        // Hash the new password before storing
        user.Auth.PasswordHash = _passwordService.HashPassword(request.NewPassword);
        user.ModifiedBy = userId;
        user.ModifiedAt = DateTime.UtcNow;
        user.Version++;

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Password changed successfully for user: {UserId}", userId);

        return true;
    }

    public async Task<bool> ResetPasswordAsync(string userId, string? requesterOrgId, bool isPlatformAdmin, string resetBy, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Resetting password for user: {UserId} by {ResetBy}", userId, resetBy);

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("User not found for password reset: {UserId}", userId);
            throw new NotFoundException("User", userId);
        }

        // Check access: Platform admin can reset all, org admin can only reset users from their org
        if (!isPlatformAdmin && user.OrgId != requesterOrgId)
        {
            _logger.LogWarning("Access denied: User tried to reset password for user {UserId} from different org", userId);
            throw new ForbiddenException("You can only reset passwords for users in your organization");
        }

        // Prevent resetting platform admin password by non-platform admin
        if (user.UserType == "platform_admin" && !isPlatformAdmin)
        {
            throw new ForbiddenException("Only platform admins can reset platform admin passwords");
        }

        // Generate new password: first 4 letters of email + "_" + first 4 letters of name (lowercase)
        var newPassword = GeneratePassword(user.Email, user.Name);
        
        // Hash the new password before storing
        user.Auth.PasswordHash = _passwordService.HashPassword(newPassword);
        user.ModifiedBy = resetBy;
        user.ModifiedAt = DateTime.UtcNow;
        user.Version++;

        await _userRepository.UpdateAsync(user, cancellationToken);

        _logger.LogInformation("Password reset successfully for user: {UserId}. New password follows standard generation pattern.", userId);

        return true;
    }
}
