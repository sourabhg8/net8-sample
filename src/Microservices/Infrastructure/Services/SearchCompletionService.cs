using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.DTOs;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

/// <summary>
/// Azure OpenAI chat completions client for featured search summaries.
/// </summary>
public class SearchCompletionService : ISearchCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly SearchCompletionSettings _settings;
    private readonly ILogger<SearchCompletionService> _logger;

    public SearchCompletionService(
        HttpClient httpClient,
        IOptions<SearchCompletionSettings> options,
        ILogger<SearchCompletionService> logger)
    {
        _httpClient = httpClient;
        _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    public async Task<string?> GetFeaturedAnswerAsync(
        string userQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        bool insufficientHighRelevanceExcerpts,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
            return null;

        var systemPrompt = """
            You help users understand biomedical search results. Produce a concise featured answer (2–5 sentences) for the user's query.
            When excerpts are provided, base your answer only on those excerpts and cite themes from them.
            If excerpts are missing or clearly insufficient for the query, give a brief general answer or state clearly that the indexed sources did not contain enough information—do not invent citations.
            Do not use markdown headings; plain text only.
            """;

        var userContent = BuildUserPrompt(userQuery, excerpts, insufficientHighRelevanceExcerpts);

        var payload = new ChatCompletionRequest
        {
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userContent }
            ],
            Temperature = 0.3,
            MaxTokens = 500
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.CompletionApiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", _settings.CompletionApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            Encoding.UTF8,
            "application/json");

        try
        {
            var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Completion API returned {Status}: {Body}",
                    (int)response.StatusCode,
                    body.Length > 500 ? body[..500] + "..." : body);
                return null;
            }

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Completion API call failed");
            return null;
        }
    }

    private static string BuildUserPrompt(string query, IReadOnlyList<SearchExcerpt> excerpts, bool insufficient)
    {
        var sb = new StringBuilder();
        sb.AppendLine("User query: ").AppendLine(query).AppendLine();
        if (insufficient)
            sb.AppendLine("Note: The search did not return enough high-confidence passages; use the following lower-relevance excerpts if any, or state that sources are insufficient.").AppendLine();
        if (excerpts.Count == 0)
        {
            sb.AppendLine("No search excerpts were returned. Answer briefly based on general knowledge only if appropriate, or say the search returned no passages.");
            return sb.ToString();
        }

        sb.AppendLine("Search excerpts (relevance % is normalized within this page):");
        for (var i = 0; i < excerpts.Count; i++)
        {
            var e = excerpts[i];
            var text = e.Text.Length > 1200 ? e.Text[..1200] + "..." : e.Text;
            sb.AppendLine($"{i + 1}. [{e.RelevancePercent:F0}%] {e.Title}");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private sealed class ChatCompletionRequest
    {
        [JsonPropertyName("messages")]
        public List<ChatMessage> Messages { get; set; } = new();

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        /// <summary>Azure OpenAI expects snake_case for this field.</summary>
        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }

    private sealed class ChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private sealed class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
