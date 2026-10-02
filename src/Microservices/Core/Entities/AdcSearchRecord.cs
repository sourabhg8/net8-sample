namespace Microservices.Core.Entities;

/// <summary>Row from the adc-chunks Azure Search index.</summary>
public class AdcSearchRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public double? SearchScore { get; set; }

    public List<string> Aliases { get; set; } = new();
    public List<string> NormalizedAliases { get; set; } = new();
    public string? Antibody { get; set; }
    public List<string> Targets { get; set; } = new();
    public string? LinkerSequence { get; set; }
    public string? LinkerType { get; set; }
    public string? LinkerCode { get; set; }
    public string? Payload { get; set; }
    public string? PayloadClass { get; set; }
    public string? TherapeuticTarget { get; set; }
    public double? Dar { get; set; }
    public List<string> Developers { get; set; } = new();
    public string? ClinicalPhase { get; set; }
    public string? DrugStatus { get; set; }
    public string? ApprovalStatus { get; set; }
    public string? ApprovalCountry { get; set; }
    public DateTimeOffset? ApprovalDate { get; set; }
    public List<string> Indications { get; set; } = new();
    public List<string> TrialIds { get; set; } = new();
    public string? PublicationReference { get; set; }
    public string? SourceUrl { get; set; }
    public string? VerificationTier { get; set; }
    public string? ValidationNote { get; set; }
    public DateTimeOffset? ValidationDate { get; set; }
}
