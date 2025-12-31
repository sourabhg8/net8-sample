using Microservices.Core.DTOs;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Service interface for search operations
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Perform a search with query sanitization
    /// </summary>
    /// <param name="request">Search request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search response with results and pagination</returns>
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sanitize a search query string
    /// </summary>
    /// <param name="query">Raw query string</param>
    /// <returns>Sanitized query string</returns>
    string SanitizeQuery(string query);
}

