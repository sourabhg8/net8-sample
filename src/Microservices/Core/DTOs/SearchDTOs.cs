using System.ComponentModel.DataAnnotations;

namespace Microservices.Core.DTOs;

/// <summary>
/// Search request DTO
/// </summary>
public class SearchRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string SearchQuery { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Optional filter by category
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional filter by type (e.g., "document", "user", "organization")
    /// </summary>
    public string? Type { get; set; }
}

/// <summary>
/// Individual search result item
/// </summary>
public class SearchResultItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // document, user, organization, etc.
    public string Category { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Highlight { get; set; } // Highlighted matching text
    public Dictionary<string, string> Metadata { get; set; } = new();
    public double RelevanceScore { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Search response with pagination
/// </summary>
public class SearchResponse
{
    public List<SearchResultItem> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
    public string SearchQuery { get; set; } = string.Empty;
    public string SanitizedQuery { get; set; } = string.Empty;
    public long SearchTimeMs { get; set; }
    public Dictionary<string, int> FacetCounts { get; set; } = new(); // Category/type counts

    public static SearchResponse Create(
        List<SearchResultItem> results,
        int totalResults,
        int pageNumber,
        int pageSize,
        string searchQuery,
        string sanitizedQuery,
        long searchTimeMs)
    {
        var totalPages = (int)Math.Ceiling((double)totalResults / pageSize);
        
        return new SearchResponse
        {
            Results = results,
            TotalResults = totalResults,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = totalPages,
            HasNextPage = pageNumber < totalPages,
            HasPreviousPage = pageNumber > 1,
            SearchQuery = searchQuery,
            SanitizedQuery = sanitizedQuery,
            SearchTimeMs = searchTimeMs
        };
    }
}

