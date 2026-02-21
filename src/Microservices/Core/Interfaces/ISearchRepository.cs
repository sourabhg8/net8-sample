using Microservices.Core.Entities;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Repository interface for search operations (hybrid full-text + vector when configured).
/// </summary>
public interface ISearchRepository
{
    /// <summary>
    /// Search for items matching the query with optional filters and pagination.
    /// </summary>
    /// <param name="sanitizedQuery">The sanitized search query (used for full-text and vector search)</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="pageSize">Number of results per page</param>
    /// <param name="category">Optional category filter (legacy)</param>
    /// <param name="type">Optional type filter (legacy)</param>
    /// <param name="filters">Optional filters and selected facet values: field name -> list of values</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Tuple of (results, totalCount)</returns>
    Task<(List<SearchableItem> Results, int TotalCount)> SearchAsync(
        string sanitizedQuery,
        int pageNumber = 1,
        int pageSize = 10,
        string? category = null,
        string? type = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get facet counts for the query, optionally scoped by the same filters as the search.
    /// </summary>
    Task<Dictionary<string, int>> GetFacetCountsAsync(
        string sanitizedQuery,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters = null,
        CancellationToken cancellationToken = default);
}

