using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Repositories;

/// <summary>
/// Placeholder when AzureSearchAdc is not configured; surfaces a clear 503 to the client.
/// </summary>
public class UnconfiguredAdcSearchRepository : IAdcSearchRepository
{
    private const string Message =
        "ADC search is not configured. Set AzureSearchAdc:Endpoint, ApiKey, and IndexName in application settings.";

    public Task<(List<AdcSearchRecord> Results, int TotalCount)> SearchAsync(
        string sanitizedQuery,
        int pageNumber,
        int pageSize,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);

    public Task<Dictionary<string, int>> GetFacetCountsAsync(
        string sanitizedQuery,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);
}
