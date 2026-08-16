using System.Text.Json;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Repositories;

/// <summary>
/// Azure AI Search implementation for the medai-pmc-chunks index.
/// Supports vector search, configurable default filters, and configurable facet options.
/// </summary>
public class AzureSearchRepository : ISearchRepository
{
    private readonly SearchClient _searchClient;
    private readonly AzureSearchSettings _settings;
    private readonly ILogger<AzureSearchRepository> _logger;

    public AzureSearchRepository(
        IOptions<AzureSearchSettings> options,
        ILogger<AzureSearchRepository> logger)
    {
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_settings.IsConfigured)
            throw new InvalidOperationException("Azure Search is not configured. Set AzureSearch:Endpoint, ApiKey, and IndexName.");

        var endpoint = new Uri(_settings.Endpoint.TrimEnd('/'));
        var credential = new AzureKeyCredential(_settings.ApiKey);
        _searchClient = new SearchClient(endpoint, _settings.IndexName, credential);
    }

    public async Task<(List<SearchableItem> Results, int TotalCount)> SearchAsync(
        string sanitizedQuery,
        int pageNumber = 1,
        int pageSize = 10,
        string? category = null,
        string? type = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var filterParts = BuildFilterExpression(category, type, filters);
        var options = new SearchOptions
        {
            Filter = filterParts.Count > 0 ? string.Join(" and ", filterParts) : null,
            Size = pageSize,
            Skip = (pageNumber - 1) * pageSize,
            IncludeTotalCount = true
        };

        // Relevance order from Azure (@search.score); no local re-sort in SearchService.
        //options.OrderBy.Add("search.score() desc");

        if (_settings.SelectFields?.Count > 0)
        {
            foreach (var field in _settings.SelectFields)
                options.Select.Add(field);
        }

        foreach (var facet in _settings.FacetFields ?? new List<string>())
            options.Facets.Add(facet);

        ConfigureVectorSearch(options, sanitizedQuery);

        var searchText = string.IsNullOrWhiteSpace(sanitizedQuery) ? "*" : sanitizedQuery;
        SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>(
            searchText,
            options,
            cancellationToken).ConfigureAwait(false);

        var results = new List<SearchableItem>();
        var totalCount = (int)(response.TotalCount ?? 0);

        await foreach (var result in response.GetResultsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var item = MapMedAiDocumentToSearchableItem(result.Document);
            if (item != null)
            {
                item.SearchScore = result.Score;
                results.Add(item);
            }
        }

        _logger.LogInformation(
            "Azure Search completed: Query='{Query}', Results={Count}, Total={Total}",
            sanitizedQuery, results.Count, totalCount);

        return (results, totalCount);
    }

    public async Task<Dictionary<string, int>> GetFacetCountsAsync(
        string sanitizedQuery,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters = null,
        CancellationToken cancellationToken = default)
    {
        var facetSpecs = _settings.FacetFields ?? new List<string>();
        if (facetSpecs.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var searchText = string.IsNullOrWhiteSpace(sanitizedQuery) ? "*" : sanitizedQuery;

        // One facet query per field, excluding that field from filters so all facet values stay visible.
        var facetTasks = facetSpecs.Select(facetSpec =>
        {
            var fieldName = GetFacetFieldName(facetSpec);
            var filterParts = BuildFilterExpression(null, null, filters, excludeFilterField: fieldName);
            var options = new SearchOptions
            {
                Filter = filterParts.Count > 0 ? string.Join(" and ", filterParts) : null,
                Size = 0,
                IncludeTotalCount = false
            };
            options.Facets.Add(facetSpec);
            ConfigureVectorSearch(options, sanitizedQuery);

            return _searchClient.SearchAsync<SearchDocument>(searchText, options, cancellationToken);
        }).ToList();

        var responses = await Task.WhenAll(facetTasks).ConfigureAwait(false);

        var facets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var response in responses)
            MergeFacetResults(facets, response);

        return facets;
    }

    public async Task<IReadOnlyList<SearchableItem>> SearchAdcChunksByDocumentTitleAsync(
        string documentTitle,
        int topN = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentTitle))
            return Array.Empty<SearchableItem>();

        var size = topN > 0 ? topN : 5;
        var filterParts = BuildFilterExpression(null, null, null);
        filterParts.Add(ODataFilterEq("title", documentTitle.Trim()));

        var options = new SearchOptions
        {
            Filter = string.Join(" and ", filterParts),
            Size = size,
            IncludeTotalCount = false
        };

        if (_settings.SelectFields?.Count > 0)
        {
            foreach (var field in _settings.SelectFields)
                options.Select.Add(field);
        }
        else
        {
            options.Select.Add("chunk_id");
            options.Select.Add("title");
            options.Select.Add("chunk");
        }

        const string adcSearchQuery =
            "ADC name antibody name payload name linker name antibody-drug conjugate ADC antibody payload linker";

        if (_settings.VectorSearchEnabled && !string.IsNullOrWhiteSpace(_settings.VectorFieldName))
        {
            options.VectorSearch = new VectorSearchOptions
            {
                Queries =
                {
                    new VectorizableTextQuery(adcSearchQuery)
                    {
                        Fields = { _settings.VectorFieldName },
                        KNearestNeighborsCount = Math.Max(size, _settings.VectorK > 0 ? _settings.VectorK : 5)
                    }
                }
            };
        }

        SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>(
            adcSearchQuery,
            options,
            cancellationToken).ConfigureAwait(false);

        var results = new List<SearchableItem>();
        await foreach (var result in response.GetResultsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var item = MapMedAiDocumentToSearchableItem(result.Document);
            if (item != null)
            {
                item.SearchScore = result.Score;
                results.Add(item);
            }
        }

        _logger.LogInformation(
            "ADC chunk search completed: Title='{Title}', Chunks={Count}",
            documentTitle, results.Count);

        return results;
    }

    private List<string> BuildFilterExpression(
        string? category,
        string? type,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters,
        string? excludeFilterField = null)
    {
        var parts = new List<string>();

        foreach (var expr in _settings.DefaultFilters ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(expr))
                parts.Add(expr.Trim());
        }

        var filterFields = _settings.FilterFields ?? new List<string>();
        if (filterFields.Count >= 1 && !string.IsNullOrWhiteSpace(category))
            parts.Add(ODataFilterEq(filterFields[0], category));
        if (filterFields.Count >= 2 && !string.IsNullOrWhiteSpace(type))
            parts.Add(ODataFilterEq(filterFields[1], type));

        if (filters != null)
        {
            foreach (var kv in filters)
            {
                if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null || kv.Value.Count == 0)
                    continue;
                if (!string.IsNullOrWhiteSpace(excludeFilterField) &&
                    string.Equals(kv.Key, excludeFilterField, StringComparison.OrdinalIgnoreCase))
                    continue;

                var values = kv.Value.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                if (values.Count == 0)
                    continue;
                if (values.Count == 1)
                    parts.Add(ODataFilterEq(kv.Key, values[0]));
                else
                    parts.Add(ODataFilterOr(kv.Key, values));
            }
        }

        return parts;
    }

    private void ConfigureVectorSearch(SearchOptions options, string? sanitizedQuery)
    {
        if (!_settings.VectorSearchEnabled || string.IsNullOrWhiteSpace(_settings.VectorFieldName))
            return;

        options.VectorSearch = new VectorSearchOptions
        {
            Queries =
            {
                new VectorizableTextQuery(sanitizedQuery ?? string.Empty)
                {
                    Fields = { _settings.VectorFieldName },
                    KNearestNeighborsCount = _settings.VectorK > 0 ? _settings.VectorK : 5
                }
            }
        };
    }

    private static string GetFacetFieldName(string facetSpec)
    {
        var comma = facetSpec.IndexOf(',');
        return comma >= 0 ? facetSpec[..comma].Trim() : facetSpec.Trim();
    }

    private static void MergeFacetResults(
        Dictionary<string, int> facets,
        SearchResults<SearchDocument> response)
    {
        if (response.Facets == null)
            return;

        foreach (var kv in response.Facets)
        {
            var fieldName = kv.Key;
            foreach (var facetResult in kv.Value ?? Array.Empty<FacetResult>())
            {
                var valueStr = facetResult.Value?.ToString() ?? string.Empty;
                var count = (int)(facetResult.Count ?? 0);
                if (!string.IsNullOrEmpty(valueStr))
                    facets[$"{fieldName}:{valueStr}"] = count;
            }
        }
    }

    private static string ODataFilterOr(string fieldName, IReadOnlyList<string> values)
    {
        var clauses = values.Select(v => ODataFilterEq(fieldName, v)).ToList();
        return "(" + string.Join(" or ", clauses) + ")";
    }

    private static string ODataFilterEq(string fieldName, string value)
    {
        var escaped = value.Replace("'", "''");
        return $"{fieldName} eq '{escaped}'";
    }

    /// <summary>
    /// Maps medai-pmc-chunks index document to SearchableItem.
    /// Fields: chunk_id, id, pmcid, pmid, title, authors, keywords, publishYear, publishDate, commercial_safe, source, text_source, sourceUrl, blobUrl, blobName, containerName, chunk.
    /// </summary>
    private static SearchableItem? MapMedAiDocumentToSearchableItem(SearchDocument doc)
    {
        try
        {
            var id = GetString(doc, "chunk_id") ?? GetString(doc, "id");
            if (string.IsNullOrEmpty(id))
                return null;

            var title = GetString(doc, "title") ?? string.Empty;
            var chunk = GetString(doc, "chunk") ?? string.Empty;
            var description = chunk.Length > 500 ? chunk[..500] + "..." : chunk;

            var tags = new List<string>();
            if (doc.TryGetValue("keywords", out var kwObj))
            {
                if (kwObj is JsonElement je && je.ValueKind == JsonValueKind.Array)
                {
                    foreach (var e in je.EnumerateArray())
                        if (e.ValueKind == JsonValueKind.String)
                            tags.Add(e.GetString() ?? string.Empty);
                }
            }

            var metadata = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(title))
                metadata["documentTitle"] = title.Trim();
            AddMeta(metadata, doc, "pmcid", "pmcid");
            AddMeta(metadata, doc, "pmid", "pmid");
            AddMeta(metadata, doc, "publishYear", "publishYear");
            AddMeta(metadata, doc, "publishDate", "publishDate");
            AddMeta(metadata, doc, "source", "source");
            AddMeta(metadata, doc, "text_source", "text_source");
            AddMeta(metadata, doc, "blobUrl", "blobUrl");
            AddMeta(metadata, doc, "blobName", "blobName");
            AddMeta(metadata, doc, "containerName", "containerName");

            var authors = ExtractStringArray(doc, "authors");
            if (authors.Count > 0)
                metadata["authors"] = string.Join(", ", authors);

            return new SearchableItem
            {
                Id = id,
                Title = title,
                Description = description,
                Content = chunk,
                Type = GetString(doc, "text_source") ?? GetString(doc, "source") ?? string.Empty,
                Category = GetString(doc, "source") ?? string.Empty,
                Url = GetString(doc, "sourceUrl") ?? string.Empty,
                ImageUrl = null,
                Tags = tags,
                Authors = authors,
                Metadata = metadata,
                ModifiedAt = null,
                IsActive = GetBool(doc, "commercial_safe", true)
            };
        }
        catch
        {
            return null;
        }
    }

    private static void AddMeta(Dictionary<string, string> metadata, SearchDocument doc, string key, string metaKey)
    {
        var v = GetString(doc, key);
        if (v != null)
            metadata[metaKey] = v;
        else if (doc.TryGetValue(key, out var obj))
            metadata[metaKey] = obj?.ToString() ?? string.Empty;
    }

    private static string? GetString(SearchDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v))
            return null;
        if (v is string s)
            return s;
        if (v is JsonElement je)
            return je.GetString();
        return v?.ToString();
    }

    private static List<string> ExtractStringArray(SearchDocument doc, string fieldName)
    {
        if (!doc.TryGetValue(fieldName, out var obj) || obj == null)
            return new List<string>();

        if (obj is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            return je.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString() ?? string.Empty)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();
        }

        if (obj is IEnumerable<string> stringEnumerable)
        {
            return stringEnumerable
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();
        }

        if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            var values = new List<string>();
            foreach (var item in enumerable)
            {
                var value = item switch
                {
                    null => null,
                    string s => s,
                    JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                    _ => item.ToString()
                };
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value.Trim());
            }
            return values;
        }

        return new List<string>();
    }

    private static bool GetBool(SearchDocument doc, string key, bool defaultValue)
    {
        if (!doc.TryGetValue(key, out var v))
            return defaultValue;
        if (v is bool b)
            return b;
        if (v is JsonElement je && je.ValueKind == JsonValueKind.True)
            return true;
        if (v is JsonElement je2 && je2.ValueKind == JsonValueKind.False)
            return false;
        return defaultValue;
    }
}
