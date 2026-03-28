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
}
