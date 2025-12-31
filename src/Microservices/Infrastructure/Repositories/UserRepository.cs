using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Repositories;

/// <summary>
/// In-memory user repository implementation (for demo purposes)
/// Will be replaced with Cosmos DB implementation
/// </summary>
public class UserRepository : IUserRepository
{
    // Dummy in-memory storage - will be replaced with Cosmos DB
    private static readonly List<User> _users = new()
    {
        // Platform Admin
        new User
        {
            Id = "usr_PLATFORM01",
            UserId = "usr_PLATFORM01",
            OrgId = "PLATFORM",
            OrgName = "Platform",
            UserType = "platform_admin",
            Role = "platform_admin",
            Status = "active",
            IsDeleted = false,
            Name = "Platform Admin",
            Email = "admin@123",
            Auth = new UserAuth { PasswordHash = "admin123" },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system",
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = "system",
            Version = 1
        },
        // Acme Corp Users (org_01HABC)
        new User
        {
            Id = "usr_ACME001",
            UserId = "usr_ACME001",
            OrgId = "org_01HABC",
            OrgName = "Acme Corp",
            UserType = "org_admin",
            Role = "org_admin",
            Status = "active",
            IsDeleted = false,
            Name = "John Smith",
            Email = "john.smith@acme.com",
            Auth = new UserAuth { PasswordHash = "password123" },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "usr_PLATFORM01",
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = "usr_PLATFORM01",
            Version = 1
        },
        new User
        {
            Id = "usr_ACME002",
            UserId = "usr_ACME002",
            OrgId = "org_01HABC",
            OrgName = "Acme Corp",
            UserType = "org_user",
            Role = "org_user",
            Status = "active",
            IsDeleted = false,
            Name = "Jane Doe",
            Email = "jane.doe@acme.com",
            Auth = new UserAuth { PasswordHash = "password123" },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "usr_ACME001",
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = "usr_ACME001",
            Version = 1
        },
        new User
        {
            Id = "usr_ACME003",
            UserId = "usr_ACME003",
            OrgId = "org_01HABC",
            OrgName = "Acme Corp",
            UserType = "org_user",
            Role = "org_user",
            Status = "suspended",
            IsDeleted = false,
            Name = "Bob Wilson",
            Email = "bob.wilson@acme.com",
            Auth = new UserAuth { PasswordHash = "password123" },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "usr_ACME001",
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = "usr_ACME001",
            Version = 1
        },
        // TechStart Solutions Users (org_02XYZD)
        new User
        {
            Id = "usr_TECH001",
            UserId = "usr_TECH001",
            OrgId = "org_02XYZD",
            OrgName = "TechStart Solutions",
            UserType = "org_admin",
            Role = "org_admin",
            Status = "active",
            IsDeleted = false,
            Name = "Alice Johnson",
            Email = "alice@techstart.io",
            Auth = new UserAuth { PasswordHash = "password123" },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "usr_PLATFORM01",
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = "usr_PLATFORM01",
            Version = 1
        },
        new User
        {
            Id = "usr_TECH002",
            UserId = "usr_TECH002",
            OrgId = "org_02XYZD",
            OrgName = "TechStart Solutions",
            UserType = "org_user",
            Role = "org_user",
            Status = "active",
            IsDeleted = false,
            Name = "Charlie Brown",
            Email = "charlie@techstart.io",
            Auth = new UserAuth { PasswordHash = "password123" },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "usr_TECH001",
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = "usr_TECH001",
            Version = 1
        },
        // Global Enterprises Users (org_03MNOP)
        new User
        {
            Id = "usr_GLOBAL001",
            UserId = "usr_GLOBAL001",
            OrgId = "org_03MNOP",
            OrgName = "Global Enterprises Ltd",
            UserType = "org_admin",
            Role = "org_admin",
            Status = "active",
            IsDeleted = false,
            Name = "David Chen",
            Email = "david@globalent.co.uk",
            Auth = new UserAuth { PasswordHash = "password123" },
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "usr_PLATFORM01",
            ModifiedAt = DateTime.UtcNow,
            ModifiedBy = "usr_PLATFORM01",
            Version = 1
        }
    };

    private readonly ILogger<UserRepository> _logger;

    public UserRepository(ILogger<UserRepository> logger)
    {
        _logger = logger;
    }

    public Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user by ID: {UserId}", id);
        var user = _users.FirstOrDefault(u => u.Id == id && !u.IsDeleted);
        return Task.FromResult(user);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user by email: {Email}", email);
        var user = _users.FirstOrDefault(u => 
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) && !u.IsDeleted);
        return Task.FromResult(user);
    }

    public Task<IEnumerable<User>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all users - Page: {Page}, PageSize: {PageSize}", page, pageSize);
        var users = _users
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult<IEnumerable<User>>(users);
    }

    public Task<IEnumerable<User>> GetByOrgIdAsync(string orgId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting users by OrgId: {OrgId} - Page: {Page}, PageSize: {PageSize}", orgId, page, pageSize);
        var users = _users
            .Where(u => u.OrgId == orgId && !u.IsDeleted)
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult<IEnumerable<User>>(users);
    }

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        var count = _users.Count(u => !u.IsDeleted);
        return Task.FromResult(count);
    }

    public Task<int> GetTotalCountByOrgIdAsync(string orgId, CancellationToken cancellationToken = default)
    {
        var count = _users.Count(u => u.OrgId == orgId && !u.IsDeleted);
        return Task.FromResult(count);
    }

    public Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating user: {Email}", user.Email);
        
        // Generate unique ID
        var uniqueId = $"usr_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        user.Id = uniqueId;
        user.UserId = uniqueId;
        user.CreatedAt = DateTime.UtcNow;
        user.ModifiedAt = DateTime.UtcNow;
        user.Version = 1;
        
        _users.Add(user);
        return Task.FromResult(user);
    }

    public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating user: {UserId}", user.Id);
        
        var existingUser = _users.FirstOrDefault(u => u.Id == user.Id);
        if (existingUser != null)
        {
            var index = _users.IndexOf(existingUser);
            user.ModifiedAt = DateTime.UtcNow;
            user.Version = existingUser.Version + 1;
            _users[index] = user;
        }
        
        return Task.FromResult(user);
    }

    public Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Soft deleting user: {UserId}", id);
        
        var user = _users.FirstOrDefault(u => u.Id == id);
        if (user != null)
        {
            user.IsDeleted = true;
            user.DeletedAt = DateTime.UtcNow;
            user.ModifiedBy = deletedBy;
            user.ModifiedAt = DateTime.UtcNow;
            user.Version++;
            return Task.FromResult(true);
        }
        
        return Task.FromResult(false);
    }

    public Task<bool> ExistsByEmailAsync(string email, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var exists = _users.Any(u => 
            u.Email.Equals(email, StringComparison.OrdinalIgnoreCase) 
            && !u.IsDeleted
            && (excludeId == null || u.Id != excludeId));
        return Task.FromResult(exists);
    }
}
