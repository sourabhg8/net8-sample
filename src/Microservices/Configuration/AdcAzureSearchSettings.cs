namespace Microservices.Configuration;

/// <summary>
/// Azure AI Search configuration for the adc-chunks index (hybrid full-text + vector on searchVector).
/// </summary>
public class AdcAzureSearchSettings
{
    public const string SectionName = "AzureSearchAdc";

    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string IndexName { get; set; } = "adc-chunks";

    /// <summary>Index field used for source facet / filter (UI label: sourceType).</summary>
    public string SourceFieldName { get; set; } = "source";

    public List<string> DefaultFilters { get; set; } = new();

    /// <summary>Example: source,count:20,sort:count</summary>
    public List<string> FacetFields { get; set; } = new();

    public List<string> SelectFields { get; set; } = new()
    {
        "id", "name", "aliases", "normalizedAliases", "antibody", "targets", "linkerSequence", "linkerType", "linkerCode",
        "payload", "payloadClass", "therapeuticTarget", "dar", "developers", "clinicalPhase",
        "drugStatus", "approvalStatus", "approvalCountry", "approvalDate", "indications", "trialIds",
        "publicationReference", "sourceUrl", "source", "verificationTier", "validationNote", "validationDate"
    };

    /// <summary>Limited searchable fields for BM25 leg of hybrid search.</summary>
    public List<string> SearchFields { get; set; } = new()
    {
        "name", "aliases", "normalizedAliases", "antibody", "targets", "linkerSequence", "linkerType",
        "linkerCode", "payload", "payloadClass", "therapeuticTarget", "developers", "searchText"
    };

    public bool VectorSearchEnabled { get; set; } = true;
    public string VectorFieldName { get; set; } = "searchVector";
    public int VectorK { get; set; } = 25;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(IndexName);
}
