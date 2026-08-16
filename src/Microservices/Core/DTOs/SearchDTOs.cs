using System.ComponentModel.DataAnnotations;

namespace Microservices.Core.DTOs;

/// <summary>
/// Search request DTO for POST /api/Search.
/// Hybrid search (full-text + vector) is used; Azure AI Search vectorizes the search term server-side.
/// </summary>
public class SearchRequest
{
    /// <summary>
    /// Search query text. Used for both full-text and vector search (Azure vectorizes this server-side).
    /// </summary>
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string SearchQuery { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    /// <summary>
    /// Optional filters and selected facet values from the frontend.
    /// Key = index field name (e.g. source, publishYear, text_source). Value = list of selected values.
    /// Example: { "source": ["PubMed"], "publishYear": ["2023", "2024"] }
    /// </summary>
    public Dictionary<string, List<string>>? Filters { get; set; }

    /// <summary>
    /// Optional. Legacy: filter by category (maps to first FilterFields entry in config, e.g. source).
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Optional. Legacy: filter by type (maps to second FilterFields entry in config, e.g. text_source).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Optional. Raw @search.score from the top result on page 1. Pass when paginating so relevance % stays
    /// consistent across pages (Azure already returns pages in relevance order).
    /// </summary>
    public double? PeakRelevanceScore { get; set; }
}

/// <summary>
/// Individual search result item
/// </summary>
public class SearchResultItem
{
    public string Id { get; set; } = string.Empty;
    /// <summary>Chunk preview (first ~55 chars of passage text).</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Original document title from the index (e.g. paper title).</summary>
    public string? DocumentTitle { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // document, user, organization, etc.
    public string Category { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Highlight { get; set; } // Highlighted matching text
    public Dictionary<string, string> Metadata { get; set; } = new();
    public double RelevanceScore { get; set; }
    /// <summary>Publication year from the search index (Azure field "publishYear", string).</summary>
    public string? Year { get; set; }
    /// <summary>Publication date from the search index (Azure field "publishDate", string).</summary>
    public string? PublishDate { get; set; }
    /// <summary>Authors from the search index (Azure field "authors", collection of strings).</summary>
    public List<string> Authors { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

/// <summary>
/// Excerpt passed to the completion API for featured answers.
/// </summary>
public sealed class SearchExcerpt
{
    public string Title { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public double RelevancePercent { get; init; }
}

/// <summary>
/// Search response with pagination
/// </summary>
public class SearchResponse
{
    /// <summary>
    /// Optional AI-generated featured summary (Google-style answer). Omitted when null or empty.
    /// </summary>
    public string? AiSummary { get; set; }

    /// <summary>
    /// Raw @search.score of the #1 result (page 1 only). Send back as PeakRelevanceScore when paginating.
    /// </summary>
    public double? PeakRelevanceScore { get; set; }

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

