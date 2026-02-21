using System.Diagnostics;
using System.Text.RegularExpressions;
using Microservices.Core.DTOs;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

/// <summary>
/// Search service implementation with query sanitization
/// </summary>
public partial class SearchService : ISearchService
{
    private readonly ISearchRepository _searchRepository;
    private readonly ILogger<SearchService> _logger;

    // Characters that could be used for injection attacks
    private static readonly char[] DangerousChars = { '<', '>', '"', '\'', '&', '\\', ';', '|', '`' };
    
    // Regex patterns for sanitization
    [GeneratedRegex(@"[<>""'&\\;|`]", RegexOptions.Compiled)]
    private static partial Regex DangerousCharsRegex();
    
    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesRegex();

    public SearchService(ISearchRepository searchRepository, ILogger<SearchService> logger)
    {
        _searchRepository = searchRepository;
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Search request received: Query='{Query}', Page={Page}, PageSize={PageSize}",
            request.SearchQuery, request.PageNumber, request.PageSize);

        // Sanitize the query
        var sanitizedQuery = SanitizeQuery(request.SearchQuery);

        _logger.LogDebug(
            "Query sanitized: Original='{Original}', Sanitized='{Sanitized}'",
            request.SearchQuery, sanitizedQuery);

        var filters = BuildFiltersFromRequest(request);

        // Perform the search (hybrid full-text + vector when Azure Search is configured; Azure vectorizes the query server-side)
        var (results, totalCount) = await _searchRepository.SearchAsync(
            sanitizedQuery,
            request.PageNumber,
            request.PageSize,
            request.Category,
            request.Type,
            filters,
            cancellationToken);

        // Get facet counts scoped by the same filters
        var facetCounts = await _searchRepository.GetFacetCountsAsync(sanitizedQuery, filters, cancellationToken);

        stopwatch.Stop();

        // Convert entities to DTOs
        var resultItems = results.Select((item, index) => new SearchResultItem
        {
            Id = item.Id,
            Title = item.Title,
            Description = item.Description,
            Type = item.Type,
            Category = item.Category,
            Url = item.Url,
            ImageUrl = item.ImageUrl,
            Highlight = GenerateHighlight(item.Content, sanitizedQuery),
            Metadata = item.Metadata,
            RelevanceScore = CalculateDisplayScore(results.Count - index, results.Count),
            CreatedAt = item.CreatedAt,
            ModifiedAt = item.ModifiedAt
        }).ToList();

        var response = SearchResponse.Create(
            resultItems,
            totalCount,
            request.PageNumber,
            request.PageSize,
            request.SearchQuery,
            sanitizedQuery,
            stopwatch.ElapsedMilliseconds);

        response.FacetCounts = facetCounts;

        _logger.LogInformation(
            "Search completed in {ElapsedMs}ms: Query='{Query}', Results={Count}, Total={Total}",
            stopwatch.ElapsedMilliseconds, sanitizedQuery, results.Count, totalCount);

        return response;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? BuildFiltersFromRequest(SearchRequest request)
    {
        if (request.Filters == null || request.Filters.Count == 0)
            return null;
        var dict = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in request.Filters)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null)
                continue;
            var list = kv.Value.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (list.Count > 0)
                dict[kv.Key] = list;
        }
        return dict.Count > 0 ? dict : null;
    }

    public string SanitizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        // Trim whitespace
        var sanitized = query.Trim();

        // Remove dangerous characters
        sanitized = DangerousCharsRegex().Replace(sanitized, "");

        // Normalize multiple spaces to single space
        sanitized = MultipleSpacesRegex().Replace(sanitized, " ");

        // Limit length to prevent DoS
        if (sanitized.Length > 200)
            sanitized = sanitized[..200];

        // Remove any SQL-like commands (basic protection)
        sanitized = RemoveSqlKeywords(sanitized);

        return sanitized.Trim();
    }

    private static string RemoveSqlKeywords(string input)
    {
        var sqlKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "UNION", "EXEC", "EXECUTE", "--", "/*", "*/" };
        
        var result = input;
        foreach (var keyword in sqlKeywords)
        {
            result = Regex.Replace(result, Regex.Escape(keyword), "", RegexOptions.IgnoreCase);
        }
        
        return result;
    }

    private static string? GenerateHighlight(string content, string query)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(query))
            return null;

        var terms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var contentLower = content.ToLower();

        // Find the first occurrence of any search term
        int bestIndex = -1;
        foreach (var term in terms)
        {
            var index = contentLower.IndexOf(term);
            if (index != -1 && (bestIndex == -1 || index < bestIndex))
            {
                bestIndex = index;
            }
        }

        if (bestIndex == -1)
            return content.Length > 150 ? content[..150] + "..." : content;

        // Create a snippet around the match
        var start = Math.Max(0, bestIndex - 50);
        var end = Math.Min(content.Length, bestIndex + 100);
        
        var snippet = content[start..end];
        
        if (start > 0) snippet = "..." + snippet;
        if (end < content.Length) snippet += "...";

        return snippet;
    }

    private static double CalculateDisplayScore(int rank, int total)
    {
        // Convert rank to a 0-100 score for display
        if (total == 0) return 0;
        return Math.Round((double)rank / total * 100, 1);
    }
}

