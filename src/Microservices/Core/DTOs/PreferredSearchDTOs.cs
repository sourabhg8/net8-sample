using System.ComponentModel.DataAnnotations;

namespace Microservices.Core.DTOs;

public class PreferredSearchTermDto
{
    public string SearchTerm { get; set; } = string.Empty;
    public DateTime SearchTermSavedAt { get; set; }
    public DateTime SearchTermLastSearchedAt { get; set; }
}

public class PreferredSearchListResponse
{
    public List<PreferredSearchTermDto> SearchTerms { get; set; } = new();
}

public class SavePreferredSearchRequest
{
    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string SearchTerm { get; set; } = string.Empty;
}
