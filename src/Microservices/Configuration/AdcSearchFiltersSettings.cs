namespace Microservices.Configuration;

/// <summary>
/// Field mapping for ADC search filters (optional rename of API key vs index field).
/// </summary>
public class AdcSearchFiltersSettings
{
    public const string SectionName = "AdcSearchFilters";

    /// <summary>Optional UI/API key; typically "drugStatus".</summary>
    public string DrugStatusParamKey { get; set; } = "drugStatus";

    /// <summary>Azure Search index field name (must be facet/filterable).</summary>
    public string DrugStatusIndexFieldName { get; set; } = "drugStatus";
}
