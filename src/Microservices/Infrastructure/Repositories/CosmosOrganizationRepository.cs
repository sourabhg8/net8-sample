using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Repositories;

/// <summary>
/// Cosmos DB implementation of organization repository
/// </summary>
public class CosmosOrganizationRepository : IOrganizationRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosOrganizationRepository> _logger;

    public CosmosOrganizationRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosOrganizationRepository> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase(settings.Value.DatabaseName);
        _container = database.GetContainer(settings.Value.OrganizationsContainerName);
    }

    public async Task<Organization?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting organization by ID: {OrgId}", id);
        
        try
        {
            // Partition key is the same as id for organizations
            var response = await _container.ReadItemAsync<Organization>(
                id, 
                new PartitionKey(id),
                cancellationToken: cancellationToken);
            
            if (response.Resource.IsDeleted)
            {
                return null;
            }
            
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogDebug("Organization not found: {OrgId}", id);
            return null;
        }
    }

    public async Task<IEnumerable<Organization>> GetAllAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all organizations - Page: {Page}, PageSize: {PageSize}", page, pageSize);

        var offset = (page - 1) * pageSize;
        
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.isDeleted = false ORDER BY c.createdAt DESC OFFSET @offset LIMIT @limit")
            .WithParameter("@offset", offset)
            .WithParameter("@limit", pageSize);

        var organizations = new List<Organization>();
        var iterator = _container.GetItemQueryIterator<Organization>(query);
        
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            organizations.AddRange(response);
        }
        
        return organizations;
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

    public async Task<Organization> CreateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Creating organization: {OrgName}", organization.Name);
        
        // Generate unique ID if not provided
        if (string.IsNullOrEmpty(organization.Id))
        {
            var uniqueId = $"org_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            organization.Id = uniqueId;
            organization.OrgId = uniqueId;
        }
        
        organization.CreatedAt = DateTime.UtcNow;
        organization.ModifiedAt = DateTime.UtcNow;
        organization.Version = 1;

        var response = await _container.CreateItemAsync(
            organization, 
            new PartitionKey(organization.Id),
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("Organization created with ID: {OrgId}, RU charge: {RUCharge}", 
            organization.Id, response.RequestCharge);
        
        return response.Resource;
    }

    public async Task<Organization> UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Updating organization: {OrgId}", organization.Id);
        
        organization.ModifiedAt = DateTime.UtcNow;
        organization.Version++;

        var response = await _container.ReplaceItemAsync(
            organization,
            organization.Id,
            new PartitionKey(organization.Id),
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("Organization updated: {OrgId}, RU charge: {RUCharge}", 
            organization.Id, response.RequestCharge);
        
        return response.Resource;
    }

    public async Task<bool> DeleteAsync(string id, string deletedBy, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Soft deleting organization: {OrgId}", id);
        
        var organization = await GetByIdAsync(id, cancellationToken);
        if (organization == null)
        {
            return false;
        }
        
        organization.IsDeleted = true;
        organization.DeletedAt = DateTime.UtcNow;
        organization.ModifiedBy = deletedBy;
        organization.ModifiedAt = DateTime.UtcNow;
        organization.Version++;

        await _container.ReplaceItemAsync(
            organization,
            organization.Id,
            new PartitionKey(organization.Id),
            cancellationToken: cancellationToken);
        
        _logger.LogInformation("Organization soft deleted: {OrgId}", id);
        return true;
    }

    public async Task<bool> ExistsByNameAsync(string name, string? excludeId = null, CancellationToken cancellationToken = default)
    {
        var queryText = excludeId != null
            ? "SELECT VALUE COUNT(1) FROM c WHERE LOWER(c.name) = LOWER(@name) AND c.isDeleted = false AND c.id != @excludeId"
            : "SELECT VALUE COUNT(1) FROM c WHERE LOWER(c.name) = LOWER(@name) AND c.isDeleted = false";

        var query = new QueryDefinition(queryText)
            .WithParameter("@name", name);

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

