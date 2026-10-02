using System.ComponentModel.DataAnnotations;

namespace Microservices.Core.DTOs;

public class AdcSearchRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string SearchQuery { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int PageNumber { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    /// <summary>UI sends sourceType; mapped to index field <c>source</c> on the server.</summary>
    public Dictionary<string, List<string>>? Filters { get; set; }

    public double? PeakRelevanceScore { get; set; }
}

/// <summary>Expanded ADC index fields for the results detail panel.</summary>
public class AdcSearchDetailsDto
{
    public string? Name { get; set; }
    public string? Aliases { get; set; }
    public string? NormalizedAliases { get; set; }
    public string? Antibody { get; set; }
    public string? Targets { get; set; }
    public string? LinkerCode { get; set; }
    public string? LinkerType { get; set; }
    public string? LinkerSequence { get; set; }
    public string? Payload { get; set; }
    public string? PayloadClass { get; set; }
    public string? TherapeuticTarget { get; set; }
    public string? Dar { get; set; }
    public string? Developers { get; set; }
    public string? ClinicalPhase { get; set; }
    public string? DrugStatus { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? ApprovalCountry { get; set; }
    public string? ApprovalDate { get; set; }
    public string? Indications { get; set; }
    public string? TrialIds { get; set; }
    public string? PublicationReference { get; set; }
    public string? Source { get; set; }
    public string? SourceUrl { get; set; }
    public string? VerificationTier { get; set; }
    public string? ValidationNote { get; set; }
    public string? ValidationDate { get; set; }
}

public class AdcSearchResultItemDto
{
    public string Id { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AdcId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public double RelevanceScore { get; set; }
    public double? SearchScore { get; set; }
    public bool IsNew { get; set; }
    public AdcSearchDetailsDto Details { get; set; } = new();
}

public class AdcSearchResponse
{
    public List<AdcSearchResultItemDto> Results { get; set; } = new();
    public int TotalResults { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage { get; set; }
    public bool HasPreviousPage { get; set; }
    public string SearchQuery { get; set; } = string.Empty;
    public string SanitizedQuery { get; set; } = string.Empty;
    public long SearchTimeMs { get; set; }
    public Dictionary<string, int> FacetCounts { get; set; } = new();
    public double? PeakRelevanceScore { get; set; }

    public static AdcSearchResponse Create(
        List<AdcSearchResultItemDto> results,
        int totalResults,
        int pageNumber,
        int pageSize,
        string searchQuery,
        string sanitizedQuery,
        long searchTimeMs)
    {
        var totalPages = totalResults == 0 ? 0 : (int)Math.Ceiling((double)totalResults / pageSize);
        return new AdcSearchResponse
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
