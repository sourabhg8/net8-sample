using Microservices.Core.Entities;

namespace Microservices.Core.Interfaces;

public interface IAdcSearchRepository
{
    Task<(List<AdcSearchRecord> Results, int TotalCount)> SearchAsync(
        string sanitizedQuery,
        int pageNumber,
        int pageSize,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, int>> GetFacetCountsAsync(
        string sanitizedQuery,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters,
        CancellationToken cancellationToken = default);
}
