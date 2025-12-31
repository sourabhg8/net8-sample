using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.Interfaces;
using User = Microservices.Core.Entities.User;

namespace Microservices.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation of user repository
/// </summary>
public class CosmosUserRepository : IUserRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosUserRepository> _logger;

    public CosmosUserRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosUserRepository> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase(settings.Value.DatabaseName);
        _container = database.GetContainer(settings.Value.UsersContainerName);
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user by ID: {UserId}", id);
        
        try
        {
            // Since we partition by orgId, we need to query across partitions for ID lookup
            var query = new QueryDefinition(
                "SELECT * FROM c WHERE c.id = @id AND c.isDeleted = false")
                .WithParameter("@id", id);

            var iterator = _container.GetItemQueryIterator<User>(query);
            
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync(cancellationToken);
                var user = response.FirstOrDefault();
                if (user != null)
                {
                    return user;
                }
            }
            
            return null;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("User not found: {UserId}", id);
            return null;
        }
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting user by email: {Email}", email);

        var query = new QueryDefinition(
            "SELECT * FROM c WHERE LOWER(c.email) = LOWER(@email) AND c.isDeleted = false")
            .WithParameter("@email", email);

        var iterator = _container.GetItemQueryIterator<User>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            var user = response.FirstOrDefault();
            if (user != null)
            {
                return user;
            }
        }
        
        return null;
    }

    public async Task<IEnumerable<User>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all users - Page: {Page}, PageSize: {PageSize}", page, pageSize);

        var offset = (page - 1) * pageSize;
        
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.isDeleted = false ORDER BY c.createdAt DESC OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var users = new List<User>();
        var iterator = _container.GetItemQueryIterator<User>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            users.AddRange(response);
        }
        
        return users;
    }

    public async Task<IEnumerable<User>> GetByOrgIdAsync(string orgId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting users by OrgId: {OrgId} - Page: {Page}, PageSize: {PageSize}", orgId, page, pageSize);

        var offset = (page - 1) * pageSize;
        
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.orgId = @orgId AND c.isDeleted = false ORDER BY c.createdAt DESC OFFSET @offset LIMIT @limit")
            .WithParameter("@orgId", orgId)
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(orgId)
        };

        var users = new List<User>();
        var iterator = _container.GetItemQueryIterator<User>(query, requestOptions: queryOptions);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            users.AddRange(response);
        }
        
        return users;
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.isDeleted = false");

        var iterator = _container.GetItemQueryIterator<int>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }
        
        return 0;
    }

    public async Task<int> GetTotalCountByOrgIdAsync(string orgId, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.orgId = @orgId AND c.isDeleted = false")
            .WithParameter("@orgId", orgId);

        var queryOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(orgId)
        };

        var iterator = _container.GetItemQueryIterator<int>(query, requestOptions: queryOptions);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault();
        }
        
        return 0;
    }

    public async Task<User> CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating user: {Email}", user.Email);
        
        // Generate unique ID if not provided
        if (string.IsNullOrEmpty(user.Id))
        {
            var uniqueId = $"usr_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            user.Id = uniqueId;
            user.UserId = uniqueId;
        }
        
        user.CreatedAt = DateTime.UtcNow;
        user.ModifiedAt = DateTime.UtcNow;
        user.Version = 1;

        var response = await _container.CreateItemAsync(
            user, 
            new PartitionKey(user.OrgId),
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("User created with ID: {UserId}, RU charge: {RUCharge}", 
            user.Id, response.RequestCharge);
        
        return response.Resource;
    }

    public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating user: {UserId}", user.Id);
        
        user.ModifiedAt = DateTime.UtcNow;
        user.Version++;

        var response = await _container.ReplaceItemAsync(
            user,
            user.Id,
            new PartitionKey(user.OrgId),
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("User updated: {UserId}, RU charge: {RUCharge}", 
            user.Id, response.RequestCharge);
        
        return response.Resource;
    }

    public async Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Soft deleting user: {UserId}", id);
        
        var user = await GetByIdAsync(id, cancellationToken);
        if (user == null)
        {
            return false;
        }
        
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        user.ModifiedBy = deletedBy;
        user.ModifiedAt = DateTime.UtcNow;
        user.Version++;

        await _container.ReplaceItemAsync(
            user,
            user.Id,
            new PartitionKey(user.OrgId),
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("User soft deleted: {UserId}", id);
        return true;
    }

    public async Task<bool> ExistsByEmailAsync(string email, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var queryText = excludeId != null
            ? "SELECT VALUE COUNT(1) FROM c WHERE LOWER(c.email) = LOWER(@email) AND c.isDeleted = false AND c.id != @excludeId"
            : "SELECT VALUE COUNT(1) FROM c WHERE LOWER(c.email) = LOWER(@email) AND c.isDeleted = false";

        var query = new QueryDefinition(queryText)
            .WithParameter("@email", email);

        if (excludeId != null)
        {
            query = query.WithParameter("@excludeId", excludeId);
        }

        var iterator = _container.GetItemQueryIterator<int>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            return response.FirstOrDefault() > 0;
        }
        
        return false;
    }
}
