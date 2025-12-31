using Microservices.Core.Entities;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Repository interface for search operations
/// </summary>
public interface ISearchRepository
{
    /// <summary>
    /// Search for items matching the query
    /// </summary>
    /// <param name="sanitizedQuery">The sanitized search query</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of results per page</param>
    /// <param name="category">Optional category filter</param>
    /// <param name="type">Optional type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (results, totalCount)</returns>
    Task<(List<SearchableItem> Results, int TotalCount)> SearchAsync(
        string sanitizedQuery,
        int pageNumber = 1,
        int pageSize = 10,
        string? category = null,
        string? type = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get facet counts for categories and types
    /// </summary>
    Task<Dictionary<string, int>> GetFacetCountsAsync(
        string sanitizedQuery,
        CancellationToken cancellationToken = default);
}

