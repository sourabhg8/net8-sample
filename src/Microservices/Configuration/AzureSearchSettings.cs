namespace Microservices.Configuration;

/// <summary>
/// Azure AI Search (formerly Azure Cognitive Search) configuration settings.
/// Supports the medai-pmc-chunks index with vector search, configurable filters and facets.
/// </summary>
public class AzureSearchSettings
{
    public const string SectionName = "AzureSearch";

    /// <summary>
    /// Azure Search service endpoint (e.g. https://my-search.search.windows.net).
    /// When empty, the application uses the in-memory mock search repository.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// API key (admin or query key) for authenticating to the search service.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Name of the search index to query (e.g. medai-pmc-chunks).
    /// </summary>
    public string IndexName { get; set; } = string.Empty;

    /// <summary>
    /// Index field names used to build OData filters from the search request
    /// (e.g. category, type). First = request Category, second = request Type.
    /// </summary>
    public List<string> FilterFields { get; set; } = new();

    /// <summary>
    /// Default OData filter expressions applied to every search (ANDed together).
    /// Example: [ "commercial_safe eq true" ] to only return commercially safe chunks.
    /// </summary>
    public List<string> DefaultFilters { get; set; } = new();

    /// <summary>
    /// Facet definitions: each entry is a field name, optionally with options.
    /// Examples: "source", "year", "source,count:10,sort:count", "text_source,count:5".
    /// </summary>
    public List<string> FacetFields { get; set; } = new();

    /// <summary>
    /// Default number of facet values to return per field when not specified in FacetFields (e.g. "field,count:10").
    /// </summary>
    public int FacetCount { get; set; } = 10;

    /// <summary>
    /// Fields to include in search results (select clause). When empty, all retrievable fields are returned.
    /// Example: id, pmcid, pmid, title, authors, keywords, year, commercial_safe, source, text_source, sourceUrl, blobUrl, blobName, containerName, chunk
    /// </summary>
    public List<string> SelectFields { get; set; } = new();

    /// <summary>
    /// When true, hybrid search is used: full-text + vector query (text vectorized server-side).
    /// </summary>
    public bool VectorSearchEnabled { get; set; }

    /// <summary>
    /// Vector field name for vector search (e.g. chunkVector). Used when VectorSearchEnabled is true.
    /// </summary>
    public string VectorFieldName { get; set; } = "chunkVector";

    /// <summary>
    /// Number of nearest neighbors for vector search (k). Used when VectorSearchEnabled is true.
    /// </summary>
    public int VectorK { get; set; } = 5;

    /// <summary>
    /// Whether Azure Search is configured and should be used instead of mock data.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint) &&
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(IndexName);
}
