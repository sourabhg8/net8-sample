using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.DTOs;
using Microservices.Core.Entities;
using Microservices.Core.Exceptions;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

public class PreferredSearchService : IPreferredSearchService
{
    private readonly IPreferredSearchRepository _repository;
    private readonly PreferredSearchSettings _settings;
    private readonly ILogger<PreferredSearchService> _logger;

    public PreferredSearchService(
        IPreferredSearchRepository repository,
        IOptions<PreferredSearchSettings> settings,
        ILogger<PreferredSearchService> logger)
    {
        _repository = repository;
        _settings = settings?.Value ?? new PreferredSearchSettings();
        _logger = logger;
    }

    public async Task<IReadOnlyList<PreferredSearchTermDto>> GetSearchTermsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByUserIdAsync(userId, cancellationToken);
        return document == null
            ? Array.Empty<PreferredSearchTermDto>()
            : MapOrdered(document);
    }

    public async Task<IReadOnlyList<PreferredSearchTermDto>> SaveSearchTermAsync(
        string userId,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
            throw new ValidationException("Search term is required.");

        var limit = Math.Max(1, _settings.AllowedSearchTermsToStore);
        var document = await _repository.GetByUserIdAsync(userId, cancellationToken)
            ?? CreateEmptyDocument(userId);

        var now = DateTime.UtcNow;
        var existing = document.SearchTerms.FirstOrDefault(t =>
            string.Equals(t.SearchTerm, term, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.SearchTerm = term;
            existing.SearchTermSavedAt = now;
            existing.SearchTermLastSearchedAt = now;
        }
        else
        {
            while (document.SearchTerms.Count >= limit)
            {
                var toRemove = document.SearchTerms
                    .OrderBy(t => t.SearchTermLastSearchedAt)
                    .ThenBy(t => t.SearchTermSavedAt)
                    .First();
                document.SearchTerms.Remove(toRemove);
            }

            document.SearchTerms.Add(new SavedSearchTerm
            {
                SearchTerm = term,
                SearchTermSavedAt = now,
                SearchTermLastSearchedAt = now
            });
        }

        document.UpdatedAt = now;
        var saved = await _repository.UpsertAsync(document, cancellationToken);

        _logger.LogInformation("Saved preferred search for user {UserId}: {SearchTerm}", userId, term);
        return MapOrdered(saved);
    }

    public async Task<IReadOnlyList<PreferredSearchTermDto>> RecordSearchTermAsync(
        string userId,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
            throw new ValidationException("Search term is required.");

        var document = await _repository.GetByUserIdAsync(userId, cancellationToken);
        if (document == null)
            return Array.Empty<PreferredSearchTermDto>();

        var existing = document.SearchTerms.FirstOrDefault(t =>
            string.Equals(t.SearchTerm, term, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
            return MapOrdered(document);

        var now = DateTime.UtcNow;
        existing.SearchTermLastSearchedAt = now;
        document.UpdatedAt = now;

        var saved = await _repository.UpsertAsync(document, cancellationToken);
        return MapOrdered(saved);
    }

    public async Task<IReadOnlyList<PreferredSearchTermDto>> DeleteSearchTermAsync(
        string userId,
        string searchTerm,
        CancellationToken cancellationToken = default)
    {
        var term = searchTerm?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(term))
            throw new ValidationException("Search term is required.");

        var document = await _repository.GetByUserIdAsync(userId, cancellationToken);
        if (document == null)
            return Array.Empty<PreferredSearchTermDto>();

        var removed = document.SearchTerms.RemoveAll(t =>
            string.Equals(t.SearchTerm, term, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
            return MapOrdered(document);

        document.UpdatedAt = DateTime.UtcNow;
        var saved = await _repository.UpsertAsync(document, cancellationToken);

        _logger.LogInformation("Deleted preferred search for user {UserId}: {SearchTerm}", userId, term);
        return MapOrdered(saved);
    }

    public async Task<IReadOnlyList<PreferredSearchTermDto>> DeleteAllSearchTermsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var document = await _repository.GetByUserIdAsync(userId, cancellationToken);
        if (document == null || document.SearchTerms.Count == 0)
            return Array.Empty<PreferredSearchTermDto>();

        document.SearchTerms.Clear();
        document.UpdatedAt = DateTime.UtcNow;
        var saved = await _repository.UpsertAsync(document, cancellationToken);

        _logger.LogInformation("Deleted all preferred searches for user {UserId}", userId);
        return MapOrdered(saved);
    }

    private static PreferredSearchDocument CreateEmptyDocument(string userId) => new()
    {
        Id = userId,
        UserId = userId,
        PartitionKey = userId,
        SearchTerms = new List<SavedSearchTerm>(),
        UpdatedAt = DateTime.UtcNow
    };

    private static IReadOnlyList<PreferredSearchTermDto> MapOrdered(PreferredSearchDocument document) =>
        document.SearchTerms
            .OrderByDescending(t => t.SearchTermLastSearchedAt)
            .Select(t => new PreferredSearchTermDto
            {
                SearchTerm = t.SearchTerm,
                SearchTermSavedAt = t.SearchTermSavedAt,
                SearchTermLastSearchedAt = t.SearchTermLastSearchedAt
            })
            .ToList();
}
