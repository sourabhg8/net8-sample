using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.DTOs;
using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

/// <summary>
/// Search service with query sanitization, relevance normalization, and optional AI featured answers.
/// </summary>
public partial class SearchService : ISearchService
{
    private readonly ISearchRepository _searchRepository;
    private readonly ISearchCompletionService _completionService;
    private readonly SearchCompletionSettings _completionSettings;
    private readonly ILogger<SearchService> _logger;

    // Characters that could be used for injection attacks
    private static readonly char[] DangerousChars = { '<', '>', '"', '\'', '&', '\\', ';', '|', '`' };
    
    [GeneratedRegex(@"[<>""'&\\;|`]", RegexOptions.Compiled)]
    private static partial Regex DangerousCharsRegex();
    
    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesRegex();

    public SearchService(
        ISearchRepository searchRepository,
        ISearchCompletionService completionService,
        IOptions<SearchCompletionSettings> completionOptions,
        ILogger<SearchService> logger)
    {
        _searchRepository = searchRepository;
        _completionService = completionService;
        _completionSettings = completionOptions?.Value ?? new SearchCompletionSettings();
        _logger = logger;
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Search request received: Query='{Query}', Page={Page}, PageSize={PageSize}",
            request.SearchQuery, request.PageNumber, request.PageSize);

        var sanitizedQuery = SanitizeQuery(request.SearchQuery);

        _logger.LogDebug(
            "Query sanitized: Original='{Original}', Sanitized='{Sanitized}'",
            request.SearchQuery, sanitizedQuery);

        var filters = BuildFiltersFromRequest(request);

        var (results, totalCount) = await _searchRepository.SearchAsync(
            sanitizedQuery,
            request.PageNumber,
            request.PageSize,
            request.Category,
            request.Type,
            filters,
            cancellationToken);

        var facetCounts = await _searchRepository.GetFacetCountsAsync(sanitizedQuery, filters, cancellationToken);

        stopwatch.Stop();

        var normalized = NormalizeRelevancePercents(results);
        var resultItems = new List<SearchResultItem>();
        for (var i = 0; i < results.Count; i++)
        {
            var item = results[i];
            var relevancePercent = normalized[i];
            resultItems.Add(new SearchResultItem
            {
                Id = item.Id,
                Title = item.Title,
                Description = item.Description,
                Type = item.Type,
                Category = item.Category,
                Url = item.Url,
                ImageUrl = item.ImageUrl,
                Highlight = GenerateHighlight(item.Content, sanitizedQuery),
                Metadata = item.Metadata,
                RelevanceScore = relevancePercent,
                CreatedAt = item.CreatedAt,
                ModifiedAt = item.ModifiedAt
            });
        }

        var response = SearchResponse.Create(
            resultItems,
            totalCount,
            request.PageNumber,
            request.PageSize,
            request.SearchQuery,
            sanitizedQuery,
            stopwatch.ElapsedMilliseconds);

        response.FacetCounts = facetCounts;

        _logger.LogInformation(
            "Search completed in {ElapsedMs}ms: Query='{Query}', Results={Count}, Total={Total}",
            stopwatch.ElapsedMilliseconds, sanitizedQuery, results.Count, totalCount);

        return await ApplyFeaturedAnswerAsync(
            response,
            sanitizedQuery,
            results,
            normalized,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<SearchResponse> ApplyFeaturedAnswerAsync(
        SearchResponse response,
        string sanitizedQuery,
        List<SearchableItem> rawResults,
        IReadOnlyList<double> relevancePercents,
        CancellationToken cancellationToken)
    {
        if (!_completionSettings.IsConfigured)
            return response;

        var threshold = _completionSettings.RelevanceThresholdPercent;
        var topN = Math.Max(1, _completionSettings.SummaryTopResultCount);

        var indexed = rawResults
            .Select((item, i) => (item, relevancePercent: relevancePercents[i]))
            .ToList();

        var highRelevance = indexed
            .Where(x => x.relevancePercent >= threshold)
            .OrderByDescending(x => x.relevancePercent)
            .Take(topN)
            .ToList();

        IReadOnlyList<SearchExcerpt> excerpts;
        bool insufficient;

        if (highRelevance.Count > 0)
        {
            excerpts = highRelevance
                .Select(x => new SearchExcerpt
                {
                    Title = x.item.Title,
                    Text = string.IsNullOrEmpty(x.item.Content) ? x.item.Description : x.item.Content,
                    RelevancePercent = x.relevancePercent
                })
                .ToList();
            insufficient = false;
        }
        else if (indexed.Count > 0)
        {
            excerpts = indexed
                .OrderByDescending(x => x.relevancePercent)
                .Take(topN)
                .Select(x => new SearchExcerpt
                {
                    Title = x.item.Title,
                    Text = string.IsNullOrEmpty(x.item.Content) ? x.item.Description : x.item.Content,
                    RelevancePercent = x.relevancePercent
                })
                .ToList();
            insufficient = true;
        }
        else
        {
            excerpts = Array.Empty<SearchExcerpt>();
            insufficient = true;
        }

        var summary = await _completionService.GetFeaturedAnswerAsync(
            sanitizedQuery,
            excerpts,
            insufficient,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(summary))
            response.AiSummary = summary.Trim();

        return response;
    }

    /// <summary>
    /// Normalizes Azure @search.score (or mock scores) to 0–100 within the current page.
    /// </summary>
    private static IReadOnlyList<double> NormalizeRelevancePercents(IReadOnlyList<SearchableItem> items)
    {
        if (items.Count == 0)
            return Array.Empty<double>();

        var rawScores = items.Select(i => i.SearchScore).ToList();
        if (rawScores.All(s => s is null or <= 0))
        {
            var n = items.Count;
            return Enumerable.Range(0, n)
                .Select(i => n == 0 ? 0.0 : Math.Round(100.0 * (n - i) / n, 1))
                .ToList();
        }

        var max = rawScores.Max(s => s ?? 0);
        if (max <= 0)
            return items.Select(_ => 0.0).ToList();

        return items
            .Select(item =>
            {
                var s = item.SearchScore ?? 0;
                return Math.Min(100, Math.Round(s / max * 100, 1));
            })
            .ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? BuildFiltersFromRequest(SearchRequest request)
    {
        if (request.Filters == null || request.Filters.Count == 0)
            return null;
        var dict = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in request.Filters)
        {
            if (string.IsNullOrWhiteSpace(kv.Key) || kv.Value == null)
                continue;
            var list = kv.Value.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
            if (list.Count > 0)
                dict[kv.Key] = list;
        }
        return dict.Count > 0 ? dict : null;
    }

    public string SanitizeQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        var sanitized = query.Trim();
        sanitized = DangerousCharsRegex().Replace(sanitized, "");
        sanitized = MultipleSpacesRegex().Replace(sanitized, " ");

        if (sanitized.Length > 200)
            sanitized = sanitized[..200];

        sanitized = RemoveSqlKeywords(sanitized);

        return sanitized.Trim();
    }

    private static string RemoveSqlKeywords(string input)
    {
        var sqlKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "DROP", "UNION", "EXEC", "EXECUTE", "--", "/*", "*/" };
        
        var result = input;
        foreach (var keyword in sqlKeywords)
        {
            result = Regex.Replace(result, Regex.Escape(keyword), "", RegexOptions.IgnoreCase);
        }
        
        return result;
    }

    private static string? GenerateHighlight(string content, string query)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(query))
            return null;

        var terms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var contentLower = content.ToLower();

        int bestIndex = -1;
        foreach (var term in terms)
        {
            var index = contentLower.IndexOf(term);
            if (index != -1 && (bestIndex == -1 || index < bestIndex))
            {
                bestIndex = index;
            }
        }

        if (bestIndex == -1)
            return content.Length > 150 ? content[..150] + "..." : content;

        var start = Math.Max(0, bestIndex - 50);
        var end = Math.Min(content.Length, bestIndex + 100);
        
        var snippet = content[start..end];
        
        if (start > 0) snippet = "..." + snippet;
        if (end < content.Length) snippet += "...";

        return snippet;
    }
}
