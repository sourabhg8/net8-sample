using System.Text.Json;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.Entities;
using Microservices.Core.Interfaces;
using Microservices.Infrastructure.Utilities;

namespace Microservices.Infrastructure.Repositories;

public class AdcAzureSearchRepository : IAdcSearchRepository
{
    private readonly SearchClient _searchClient;
    private readonly AdcAzureSearchSettings _settings;
    private readonly ILogger<AdcAzureSearchRepository> _logger;

    public AdcAzureSearchRepository(
        IOptions<AdcAzureSearchSettings> options,
        ILogger<AdcAzureSearchRepository> logger)
    {
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_settings.IsConfigured)
            throw new InvalidOperationException(
                "ADC Azure Search is not configured. Set AzureSearchAdc:Endpoint, ApiKey, and IndexName.");

        var endpoint = new Uri(_settings.Endpoint.TrimEnd('/'));
        _searchClient = new SearchClient(endpoint, _settings.IndexName, new AzureKeyCredential(_settings.ApiKey));
    }

    public async Task<(List<AdcSearchRecord> Results, int TotalCount)> SearchAsync(
        string sanitizedQuery,
        int pageNumber,
        int pageSize,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters,
        CancellationToken cancellationToken = default)
    {
        var filterParts = BuildFilterExpression(filters);
        var options = new SearchOptions
        {
            Filter = filterParts.Count > 0 ? string.Join(" and ", filterParts) : null,
            Size = pageSize,
            Skip = (pageNumber - 1) * pageSize,
            IncludeTotalCount = true
        };

        foreach (var field in _settings.SelectFields ?? new List<string>())
            options.Select.Add(field);

        foreach (var field in _settings.SearchFields ?? new List<string>())
            options.SearchFields.Add(field);

        // Facets are loaded via GetFacetCountsAsync only (avoids duplicate facet specs on the results query).
        ConfigureVectorSearch(options, sanitizedQuery);

        var searchText = string.IsNullOrWhiteSpace(sanitizedQuery) ? "*" : sanitizedQuery;
        SearchResults<SearchDocument> response = await _searchClient.SearchAsync<SearchDocument>(
            searchText,
            options,
            cancellationToken).ConfigureAwait(false);

        var results = new List<AdcSearchRecord>();
        await foreach (var result in response.GetResultsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            var mapped = MapDocument(result.Document);
            if (mapped != null)
            {
                mapped.SearchScore = result.Score;
                results.Add(mapped);
            }
        }

        var totalCount = (int)(response.TotalCount ?? 0);
        _logger.LogInformation(
            "ADC Azure Search: Query='{Query}', Page={Page}, Count={Count}, Total={Total}",
            sanitizedQuery, pageNumber, results.Count, totalCount);

        return (results, totalCount);
    }

    public async Task<Dictionary<string, int>> GetFacetCountsAsync(
        string sanitizedQuery,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters,
        CancellationToken cancellationToken = default)
    {
        var facetSpecs = GetDistinctFacetSpecs();
        if (facetSpecs.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var searchText = string.IsNullOrWhiteSpace(sanitizedQuery) ? "*" : sanitizedQuery;
        var facetTasks = facetSpecs.Select(facetSpec =>
        {
            var fieldName = GetFacetFieldName(facetSpec);
            var filterParts = BuildFilterExpression(filters, excludeFilterField: fieldName);
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

    private List<string> BuildFilterExpression(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? filters,
        string? excludeFilterField = null)
    {
        var parts = new List<string>();
        foreach (var expr in _settings.DefaultFilters ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(expr))
                parts.Add(expr.Trim());
        }

        if (filters == null)
            return parts;

        foreach (var kv in filters)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null || kv.Value.Count == 0)
                continue;

            var fieldName = MapFilterFieldName(kv.Key);
            if (!string.IsNullOrWhiteSpace(excludeFilterField) &&
                string.Equals(fieldName, excludeFilterField, StringComparison.OrdinalIgnoreCase))
                continue;

            var values = kv.Value.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (values.Count == 0)
                continue;

            if (values.Count == 1)
                parts.Add(ODataFilterEq(fieldName, values[0]));
            else
                parts.Add(ODataFilterOr(fieldName, values));
        }

        return parts;
    }

    private string MapFilterFieldName(string requestField) =>
        string.Equals(requestField, "sourceType", StringComparison.OrdinalIgnoreCase)
            ? _settings.SourceFieldName
            : requestField;

    /// <summary>
    /// Merged appsettings can repeat the same facet field; Azure rejects duplicate facet specs per request.
    /// </summary>
    private List<string> GetDistinctFacetSpecs()
    {
        var specs = _settings.FacetFields ?? new List<string>();
        if (specs.Count == 0)
            specs = new List<string> { "source,count:20,sort:count" };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinct = new List<string>();
        foreach (var spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec))
                continue;
            var field = GetFacetFieldName(spec);
            if (seen.Add(field))
                distinct.Add(spec.Trim());
        }
        return distinct;
    }

    private void ConfigureVectorSearch(SearchOptions options, string? sanitizedQuery)
    {
        if (!_settings.VectorSearchEnabled ||
            string.IsNullOrWhiteSpace(_settings.VectorFieldName) ||
            string.IsNullOrWhiteSpace(sanitizedQuery))
            return;

        var k = _settings.VectorK > 0 ? _settings.VectorK : 25;
        options.VectorSearch = new VectorSearchOptions
        {
            Queries =
            {
                new VectorizableTextQuery(sanitizedQuery)
                {
                    Fields = { _settings.VectorFieldName },
                    KNearestNeighborsCount = k
                }
            }
        };
    }

    private static string GetFacetFieldName(string facetSpec)
    {
        var comma = facetSpec.IndexOf(',');
        return comma >= 0 ? facetSpec[..comma].Trim() : facetSpec.Trim();
    }

    private static void MergeFacetResults(Dictionary<string, int> facets, SearchResults<SearchDocument> response)
    {
        if (response.Facets == null)
            return;

        foreach (var kv in response.Facets)
        {
            foreach (var facetResult in kv.Value ?? Array.Empty<FacetResult>())
            {
                var valueStr = facetResult.Value?.ToString() ?? string.Empty;
                var count = (int)(facetResult.Count ?? 0);
                if (!string.IsNullOrEmpty(valueStr))
                    facets[$"{kv.Key}:{valueStr}"] = count;
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

    private static AdcSearchRecord? MapDocument(SearchDocument doc)
    {
        var id = SearchDocumentReader.GetString(doc, "id");
        if (string.IsNullOrEmpty(id))
            return null;

        return new AdcSearchRecord
        {
            Id = id,
            Name = SearchDocumentReader.GetStringOrJoinedArray(doc, "name") ?? string.Empty,
            Source = SearchDocumentReader.GetStringOrJoinedArray(doc, "source") ?? string.Empty,
            Aliases = SearchDocumentReader.ExtractStringList(doc, "aliases"),
            NormalizedAliases = SearchDocumentReader.ExtractStringList(doc, "normalizedAliases"),
            Antibody = SearchDocumentReader.GetStringOrJoinedArray(doc, "antibody"),
            Targets = SearchDocumentReader.ExtractStringList(doc, "targets"),
            LinkerSequence = SearchDocumentReader.GetStringOrJoinedArray(doc, "linkerSequence"),
            LinkerType = SearchDocumentReader.GetStringOrJoinedArray(doc, "linkerType"),
            LinkerCode = SearchDocumentReader.GetStringOrJoinedArray(doc, "linkerCode"),
            Payload = SearchDocumentReader.GetStringOrJoinedArray(doc, "payload"),
            PayloadClass = SearchDocumentReader.GetStringOrJoinedArray(doc, "payloadClass"),
            TherapeuticTarget = SearchDocumentReader.GetStringOrJoinedArray(doc, "therapeuticTarget"),
            Dar = GetDouble(doc, "dar"),
            Developers = SearchDocumentReader.ExtractStringList(doc, "developers"),
            ClinicalPhase = SearchDocumentReader.GetStringOrJoinedArray(doc, "clinicalPhase"),
            DrugStatus = SearchDocumentReader.GetStringOrJoinedArray(doc, "drugStatus"),
            ApprovalStatus = SearchDocumentReader.GetStringOrJoinedArray(doc, "approvalStatus"),
            ApprovalCountry = SearchDocumentReader.GetStringOrJoinedArray(doc, "approvalCountry"),
            ApprovalDate = GetDateTimeOffset(doc, "approvalDate"),
            Indications = SearchDocumentReader.ExtractStringList(doc, "indications"),
            TrialIds = SearchDocumentReader.ExtractStringList(doc, "trialIds"),
            PublicationReference = SearchDocumentReader.GetStringOrJoinedArray(doc, "publicationReference"),
            SourceUrl = SearchDocumentReader.GetStringOrJoinedArray(doc, "sourceUrl"),
            VerificationTier = SearchDocumentReader.GetStringOrJoinedArray(doc, "verificationTier"),
            ValidationNote = SearchDocumentReader.GetStringOrJoinedArray(doc, "validationNote"),
            ValidationDate = GetDateTimeOffset(doc, "validationDate")
        };
    }

    private static double? GetDouble(SearchDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v == null)
            return null;
        if (v is double d)
            return d;
        if (v is float f)
            return f;
        if (v is JsonElement je && je.ValueKind == JsonValueKind.Number && je.TryGetDouble(out var jd))
            return jd;
        return double.TryParse(v.ToString(), out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? GetDateTimeOffset(SearchDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v == null)
            return null;
        if (v is DateTimeOffset dto)
            return dto;
        if (v is DateTime dt)
            return new DateTimeOffset(dt);
        if (v is JsonElement je && je.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(je.GetString(), out var parsed))
            return parsed;
        return DateTimeOffset.TryParse(v.ToString(), out var parsed2) ? parsed2 : null;
    }

}
