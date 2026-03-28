namespace Microservices.Configuration;

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
    /// Minimum normalized relevance (0–100) for a result to be included in the summary context (default 70).
    /// </summary>
    public double RelevanceThresholdPercent { get; set; } = 70;

    /// <summary>
    /// Maximum number of high-relevance results to pass into the summary prompt (default 5).
    /// </summary>
    public int SummaryTopResultCount { get; set; } = 5;

    /// <summary>
    /// When false, no completion calls are made and AiSummary is not returned.
    /// </summary>
    public bool Enabled { get; set; } = true;

    public bool IsConfigured =>
        Enabled &&
        !string.IsNullOrWhiteSpace(CompletionApiUrl) &&
        !string.IsNullOrWhiteSpace(CompletionApiKey);
}
