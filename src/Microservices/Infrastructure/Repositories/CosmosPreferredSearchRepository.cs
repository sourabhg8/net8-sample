using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Repositories;

public class CosmosPreferredSearchRepository : IPreferredSearchRepository
{
    private readonly Container _container;
    private readonly ILogger<CosmosPreferredSearchRepository> _logger;

    public CosmosPreferredSearchRepository(
        CosmosClient cosmosClient,
        IOptions<CosmosDbSettings> settings,
        ILogger<CosmosPreferredSearchRepository> logger)
    {
        _logger = logger;
        var database = cosmosClient.GetDatabase(settings.Value.DatabaseName);
        _container = database.GetContainer(settings.Value.PreferredSearchesContainerName);
    }

    public async Task<PreferredSearchDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _container.ReadItemAsync<PreferredSearchDocument>(
                userId,
                new PartitionKey(userId),
                cancellationToken: cancellationToken);

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<PreferredSearchDocument> UpsertAsync(
        PreferredSearchDocument document,
        CancellationToken cancellationToken = default)
    {
        var response = await _container.UpsertItemAsync(
            document,
            new PartitionKey(document.PartitionKey),
            cancellationToken: cancellationToken);

        _logger.LogDebug("Upserted preferred searches for user {UserId}", document.UserId);
        return response.Resource;
    }
}
