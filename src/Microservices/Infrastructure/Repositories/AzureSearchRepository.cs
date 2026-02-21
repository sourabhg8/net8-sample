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

        if (_settings.SelectFields?.Count > 0)
        {
            foreach (var field in _settings.SelectFields)
                options.Select.Add(field);
        }

        foreach (var facet in _settings.FacetFields ?? new List<string>())
            options.Facets.Add(facet);

        // Hybrid search: full-text (searchText) + vector query. Azure AI Search vectorizes the text server-side.
        if (_settings.VectorSearchEnabled && !string.IsNullOrWhiteSpace(_settings.VectorFieldName))
        {
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
                results.Add(item);
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
        var filterParts = BuildFilterExpression(null, null, filters);
        var options = new SearchOptions
        {
            Filter = filterParts.Count > 0 ? string.Join(" and ", filterParts) : null,
            Size = 0,
            IncludeTotalCount = false
        };

        foreach (var facet in _settings.FacetFields ?? new List<string>())
            options.Facets.Add(facet);

        var searchText = string.IsNullOrWhiteSpace(sanitizedQuery) ? "*" : sanitizedQuery;
        SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>(
            searchText,
            options,
            cancellationToken).ConfigureAwait(false);

        var facets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (response.Facets != null)
        {
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

        return facets;
    }

    private List<string> BuildFilterExpression(
        string? category,
        string? type,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters)
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
    /// Fields: chunk_id, id, pmcid, pmid, title, authors, keywords, year, commercial_safe, source, text_source, sourceUrl, blobUrl, blobName, containerName, chunk.
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
            AddMeta(metadata, doc, "pmcid", "pmcid");
            AddMeta(metadata, doc, "pmid", "pmid");
            AddMeta(metadata, doc, "year", "year");
            AddMeta(metadata, doc, "source", "source");
            AddMeta(metadata, doc, "text_source", "text_source");
            AddMeta(metadata, doc, "blobUrl", "blobUrl");
            AddMeta(metadata, doc, "blobName", "blobName");
            AddMeta(metadata, doc, "containerName", "containerName");
            if (doc.TryGetValue("authors", out var authorsObj) && authorsObj is JsonElement ae && ae.ValueKind == JsonValueKind.Array)
            {
                var authorList = new List<string>();
                foreach (var e in ae.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String)
                        authorList.Add(e.GetString() ?? string.Empty);
                metadata["authors"] = string.Join("; ", authorList);
            }

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
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow,
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
