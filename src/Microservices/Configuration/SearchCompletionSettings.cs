namespace Microservices.Configuration;

/// <summary>
/// Maps a minimum raw @search.score to a fixed display relevance % (absolute tier).
/// Tiers are evaluated highest <see cref="MinRawScore"/> first.
/// </summary>
public class RelevanceScoreTier
{
    /// <summary>Minimum raw @search.score (inclusive) for this tier.</summary>
    public double MinRawScore { get; set; }

    /// <summary>Display relevance % when raw score is in this tier.</summary>
    public double DisplayPercent { get; set; }
}

/// <summary>
/// Azure OpenAI chat completion settings for AI search summaries (featured answer).
/// </summary>
public class SearchCompletionSettings
{
    public const string SectionName = "SearchCompletion";

    /// <summary>
    /// Full URL for chat completions (deployment + api-version query string).
    /// </summary>
    public string CompletionApiUrl { get; set; } = string.Empty;

    /// <summary>
    /// API key (use User Secrets or Key Vault in production).
    /// </summary>
    public string CompletionApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Legacy setting; summary always uses the top <see cref="SummaryTopResultCount"/> results on page 1.
    /// </summary>
    public double RelevanceThresholdPercent { get; set; } = 70;

    /// <summary>
    /// Maximum relevance % shown in the UI. The top hit is scaled to this cap, not 100 (default 90).
    /// </summary>
    public double RelevanceDisplayMaxPercent { get; set; } = 90;

    /// <summary>
    /// Fallback linear absolute scale when <see cref="RelevanceScoreTiers"/> is empty.
    /// </summary>
    public double? RelevanceScoreAnchor { get; set; }

    /// <summary>
    /// Raw score → display % tiers for hybrid relevance (e.g. ≥0.04 → 90%, ≥0.025 → 70%, else 50%).
    /// </summary>
    public List<RelevanceScoreTier> RelevanceScoreTiers { get; set; } = new()
    {
        new RelevanceScoreTier { MinRawScore = 0.04, DisplayPercent = 90 },
        new RelevanceScoreTier { MinRawScore = 0.025, DisplayPercent = 70 },
        new RelevanceScoreTier { MinRawScore = 0, DisplayPercent = 50 }
    };

    /// <summary>
    /// Number of top page-1 results passed into the AI summary prompt (default 5).
    /// </summary>
    public int SummaryTopResultCount { get; set; } = 5;

    /// <summary>
    /// Number of chunks to retrieve for document ADC info (default 5).
    /// </summary>
    public int DocumentAdcTopChunkCount { get; set; } = 5;

    /// <summary>
    /// When false, no completion calls are made and AiSummary is not returned.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(CompletionApiUrl) &&
        !string.IsNullOrWhiteSpace(CompletionApiKey);
}
