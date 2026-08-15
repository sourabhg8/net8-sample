using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.DTOs;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

public class DocumentAdcInfoService : IDocumentAdcInfoService
{
    private readonly ISearchRepository _searchRepository;
    private readonly ISearchCompletionService _completionService;
    private readonly SearchCompletionSettings _settings;
    private readonly ILogger<DocumentAdcInfoService> _logger;

    public DocumentAdcInfoService(
        ISearchRepository searchRepository,
        ISearchCompletionService completionService,
        IOptions<SearchCompletionSettings> options,
        ILogger<DocumentAdcInfoService> logger)
    {
        _searchRepository = searchRepository;
        _completionService = completionService;
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<DocumentAdcInfoResponse> GetDocumentAdcInfoAsync(
        string documentTitle,
        string? searchQuery = null,
        CancellationToken cancellationToken = default)
    {
        var title = documentTitle?.Trim() ?? string.Empty;
        var query = searchQuery?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            return new DocumentAdcInfoResponse
            {
                DocumentTitle = title,
                Summary = "Document title is required."
            };
        }

        if (!_settings.IsConfigured)
        {
            _logger.LogWarning("SearchCompletion is not configured; cannot generate document summary");
            return new DocumentAdcInfoResponse
            {
                DocumentTitle = title,
                Summary = "AI summary is not configured. Set SearchCompletion:CompletionApiKey in application settings."
            };
        }

        var topN = _settings.DocumentAdcTopChunkCount > 0 ? _settings.DocumentAdcTopChunkCount : 5;
        var chunks = await _searchRepository
            .SearchAdcChunksByDocumentTitleAsync(title, topN, cancellationToken)
            .ConfigureAwait(false);

        if (chunks.Count == 0)
        {
            _logger.LogInformation(
                "No ADC chunks found for document title '{Title}', searchQuery='{Query}'",
                title, query);
            return new DocumentAdcInfoResponse
            {
                DocumentTitle = title,
                Summary = "No matching document chunks were found for this title."
            };
        }

        var excerpts = chunks
            .Select(c => new SearchExcerpt
            {
                Title = c.Title,
                Text = !string.IsNullOrWhiteSpace(c.Content) ? c.Content : c.Description,
                RelevancePercent = 0
            })
            .ToList();

        var summary = await GetAiSummaryAsync(title, query, excerpts, cancellationToken).ConfigureAwait(false);

        var extracted = await _completionService
            .GetDocumentAdcInfoAsync(title, query, excerpts, cancellationToken)
            .ConfigureAwait(false);

        if (extracted != null && HasAnyAdcField(extracted))
        {
            _logger.LogDebug(
                "ADC fields extracted for '{Title}': Adc={Adc}, Antibody={Antibody}, Payload={Payload}, Linker={Linker}",
                title, extracted.AdcName, extracted.AntibodyName, extracted.PayloadName, extracted.LinkerName);
        }
        else
        {
            _logger.LogInformation("No ADC fields extracted from completion API for '{Title}'", title);
        }

        return new DocumentAdcInfoResponse
        {
            DocumentTitle = title,
            Summary = summary,
            AdcName = extracted?.AdcName ?? string.Empty,
            AntibodyName = extracted?.AntibodyName ?? string.Empty,
            PayloadName = extracted?.PayloadName ?? string.Empty,
            LinkerName = extracted?.LinkerName ?? string.Empty
        };
    }

    private async Task<string> GetAiSummaryAsync(
        string documentTitle,
        string? searchQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        CancellationToken cancellationToken)
    {
        var summary = await _completionService
            .GetDocumentChunkSummaryAsync(documentTitle, searchQuery, excerpts, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(summary))
            return summary.Trim();

        _logger.LogWarning(
            "All completion attempts failed for document summary. Title='{Title}'",
            documentTitle);

        return "Unable to generate a summary at this time.";
    }

    private static bool HasAnyAdcField(DocumentAdcInfoResponse response) =>
        !string.IsNullOrWhiteSpace(response.AdcName)
        || !string.IsNullOrWhiteSpace(response.AntibodyName)
        || !string.IsNullOrWhiteSpace(response.PayloadName)
        || !string.IsNullOrWhiteSpace(response.LinkerName);
}
