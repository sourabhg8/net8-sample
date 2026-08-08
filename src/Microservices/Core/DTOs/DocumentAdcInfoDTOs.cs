using System.ComponentModel.DataAnnotations;

namespace Microservices.Core.DTOs;

public class DocumentAdcInfoRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string DocumentTitle { get; set; } = string.Empty;

    [StringLength(500)]
    public string? SearchQuery { get; set; }
}

public class DocumentAdcInfoResponse
{
    public string DocumentTitle { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string AdcName { get; set; } = string.Empty;
    public string AntibodyName { get; set; } = string.Empty;
    public string PayloadName { get; set; } = string.Empty;
    public string LinkerName { get; set; } = string.Empty;
}
