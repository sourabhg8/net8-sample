using System.Diagnostics;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.DTOs;
using Microservices.Core.Entities;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

public class AdcSearchService : IAdcSearchService
{
    private readonly IAdcSearchRepository _repository;
    private readonly ISearchService _searchService;
    private readonly AdcSearchRelevanceSettings _relevanceSettings;
    private readonly AdcAzureSearchSettings _adcSearchSettings;
    private readonly ILogger<AdcSearchService> _logger;

    public AdcSearchService(
        IAdcSearchRepository repository,
        ISearchService searchService,
        IOptions<AdcSearchRelevanceSettings> relevanceOptions,
        IOptions<AdcAzureSearchSettings> adcSearchOptions,
        ILogger<AdcSearchService> logger)
    {
        _repository = repository;
        _searchService = searchService;
        _relevanceSettings = relevanceOptions?.Value ?? new AdcSearchRelevanceSettings();
        _adcSearchSettings = adcSearchOptions?.Value ?? new AdcAzureSearchSettings();
        _logger = logger;
    }

    public async Task<AdcSearchResponse> SearchAsync(AdcSearchRequest request, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var sanitizedQuery = _searchService.SanitizeQuery(request.SearchQuery);
        var filters = BuildFiltersFromRequest(request);

        var (records, totalCount) = await _repository.SearchAsync(
            sanitizedQuery,
            request.PageNumber,
            request.PageSize,
            filters,
            cancellationToken).ConfigureAwait(false);

        var facetCountsRaw = await _repository.GetFacetCountsAsync(sanitizedQuery, filters, cancellationToken)
            .ConfigureAwait(false);
        var facetCounts = MapFacetKeysForUi(facetCountsRaw);

        var peakRawScore = request.PeakRelevanceScore;
        if (request.PageNumber == 1 && records.Count > 0 && records[0].SearchScore is > 0)
            peakRawScore = records[0].SearchScore;

        var normalized = NormalizeRelevancePercents(records, peakRawScore);
        var startRank = (request.PageNumber - 1) * request.PageSize;

        var items = new List<AdcSearchResultItemDto>();
        for (var i = 0; i < records.Count; i++)
        {
            var r = records[i];
            items.Add(MapToDto(r, startRank + i + 1, normalized[i]));
        }

        stopwatch.Stop();

        var response = AdcSearchResponse.Create(
            items,
            totalCount,
            request.PageNumber,
            request.PageSize,
            request.SearchQuery,
            sanitizedQuery,
            stopwatch.ElapsedMilliseconds);

        response.FacetCounts = facetCounts;
        if (request.PageNumber == 1 && peakRawScore is > 0)
            response.PeakRelevanceScore = peakRawScore;

        _logger.LogInformation(
            "ADC search completed in {Ms}ms: Query='{Query}', Total={Total}",
            stopwatch.ElapsedMilliseconds, sanitizedQuery, totalCount);

        return response;
    }

    private Dictionary<string, int> MapFacetKeysForUi(Dictionary<string, int> raw)
    {
        var sourceField = _adcSearchSettings.SourceFieldName ?? "source";
        var mapped = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in raw)
        {
            var key = kv.Key;
            if (key.StartsWith($"{sourceField}:", StringComparison.OrdinalIgnoreCase))
                key = "sourceType" + key[sourceField.Length..];
            mapped[key] = kv.Value;
        }
        return mapped;
    }

    private static AdcSearchResultItemDto MapToDto(
        AdcSearchRecord r,
        int rank,
        double relevancePercent)
    {
        return new AdcSearchResultItemDto
        {
            Id = r.Id,
            Rank = rank,
            Title = r.Name,
            AdcId = r.Id,
            SourceType = string.IsNullOrWhiteSpace(r.Source) ? "Other" : r.Source,
            RelevanceScore = relevancePercent,
            SearchScore = r.SearchScore,
            IsNew = false,
            Details = new AdcSearchDetailsDto
            {
                Name = r.Name,
                Aliases = JoinList(r.Aliases),
                NormalizedAliases = JoinList(r.NormalizedAliases),
                Antibody = r.Antibody,
                Targets = JoinList(r.Targets),
                LinkerCode = r.LinkerCode,
                LinkerType = r.LinkerType,
                LinkerSequence = r.LinkerSequence,
                Payload = r.Payload,
                PayloadClass = r.PayloadClass,
                TherapeuticTarget = r.TherapeuticTarget,
                Dar = r.Dar.HasValue ? r.Dar.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
                Developers = JoinList(r.Developers),
                ClinicalPhase = r.ClinicalPhase,
                DrugStatus = r.DrugStatus,
                ApprovalStatus = r.ApprovalStatus,
                ApprovalCountry = r.ApprovalCountry,
                ApprovalDate = FormatDate(r.ApprovalDate),
                Indications = JoinList(r.Indications, "; "),
                TrialIds = JoinList(r.TrialIds),
                PublicationReference = r.PublicationReference,
                Source = r.Source,
                SourceUrl = r.SourceUrl,
                VerificationTier = r.VerificationTier,
                ValidationNote = r.ValidationNote,
                ValidationDate = FormatDate(r.ValidationDate)
            }
        };
    }

    private static string? JoinList(IReadOnlyList<string> values, string separator = ", ") =>
        values.Count > 0 ? string.Join(separator, values) : null;

    private static string? FormatDate(DateTimeOffset? date) =>
        date.HasValue ? date.Value.ToString("yyyy-MM-dd") : null;

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? BuildFiltersFromRequest(AdcSearchRequest request)
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

    private List<double> NormalizeRelevancePercents(IReadOnlyList<AdcSearchRecord> items, double? peakRawScore)
    {
        var maxDisplay = _relevanceSettings.RelevanceDisplayMaxPercent;
        if (maxDisplay <= 0)
            maxDisplay = 90;
        maxDisplay = Math.Min(maxDisplay, 100);

        if (items.Count == 0)
            return new List<double>();

        var rawScores = items.Select(i => i.SearchScore).ToList();
        if (rawScores.All(s => s is null or <= 0))
        {
            var n = items.Count;
            return Enumerable.Range(0, n)
                .Select(i => n == 0 ? 0.0 : Math.Round(maxDisplay * (n - i) / n, 2))
                .ToList();
        }

        var peak = peakRawScore ?? rawScores.Max(s => s ?? 0);
        if (peak <= 0)
            return items.Select(_ => 0.0).ToList();

        var peakDisplayPercent = MapRawScoreToAbsolutePercent(peak, maxDisplay) ?? maxDisplay;

        return items
            .Select(item =>
            {
                var s = item.SearchScore ?? 0;
                var display = s / peak * peakDisplayPercent;
                return Math.Round(Math.Clamp(display, 0, maxDisplay), 2);
            })
            .ToList();
    }

    private double? MapRawScoreToAbsolutePercent(double rawScore, double maxDisplay)
    {
        var tiers = _relevanceSettings.RelevanceScoreTiers?
            .Where(t => t.DisplayPercent > 0)
            .OrderByDescending(t => t.MinRawScore)
            .ToList();

        if (tiers is { Count: > 0 })
        {
            foreach (var tier in tiers)
            {
                if (rawScore >= tier.MinRawScore)
                    return Math.Min(maxDisplay, tier.DisplayPercent);
            }

            var lowest = tiers.MinBy(t => t.MinRawScore);
            return lowest != null ? Math.Min(maxDisplay, lowest.DisplayPercent) : null;
        }

        var anchor = _relevanceSettings.RelevanceScoreAnchor;
        if (anchor is > 0)
            return Math.Min(maxDisplay, rawScore / anchor.Value * 100);

        return null;
    }
}
