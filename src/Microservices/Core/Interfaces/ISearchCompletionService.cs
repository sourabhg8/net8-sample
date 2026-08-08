using Microservices.Core.DTOs;

namespace Microservices.Core.Interfaces;

/// <summary>
/// Calls Azure OpenAI chat completions to produce a featured search summary.
/// </summary>
public interface ISearchCompletionService
{
    /// <summary>
    /// Generates a short summary from high-relevance excerpts, or a fallback answer when excerpts are insufficient.
    /// Returns null if the call fails or produces empty content.
    /// </summary>
    Task<string?> GetFeaturedAnswerAsync(
        string userQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        bool insufficientHighRelevanceExcerpts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Summarizes ADC-related fields from document chunks (JSON structured response).
    /// </summary>
    Task<DocumentAdcInfoResponse?> GetDocumentAdcInfoAsync(
        string documentTitle,
        string? searchQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a plain-text document summary (~100–120 words) from chunks via completion API.
    /// </summary>
    Task<string?> GetDocumentChunkSummaryAsync(
        string documentTitle,
        string? searchQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        CancellationToken cancellationToken = default);
}
