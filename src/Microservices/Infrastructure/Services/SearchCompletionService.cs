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
    private static readonly JsonSerializerOptions RequestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions ResponseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

        return await SendCompletionAsync(
            systemPrompt,
            userContent,
            temperature: 0.3,
            maxTokens: 500,
            useJsonFormat: false,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<DocumentAdcInfoResponse?> GetDocumentAdcInfoAsync(
        string documentTitle,
        string? searchQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured)
            return null;

        var systemPrompt = BuildAdcSystemPrompt();
        var userContent = BuildAdcExtractionPrompt(documentTitle, searchQuery, excerpts);

        var text = await SendCompletionAsync(
            systemPrompt,
            userContent,
            temperature: 0.2,
            maxTokens: 900,
            useJsonFormat: true,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation("ADC info JSON completion failed; retrying without response_format");
            text = await SendCompletionAsync(
                systemPrompt,
                userContent,
                temperature: 0.2,
                maxTokens: 900,
                useJsonFormat: false,
                cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var parsed = ParseAdcInfoResponse(text);
        if (parsed != null && !string.IsNullOrWhiteSpace(parsed.Summary))
            return parsed;

        return new DocumentAdcInfoResponse
        {
            Summary = parsed?.Summary ?? text.Trim(),
            AdcName = parsed?.AdcName ?? string.Empty,
            AntibodyName = parsed?.AntibodyName ?? string.Empty,
            PayloadName = parsed?.PayloadName ?? string.Empty,
            LinkerName = parsed?.LinkerName ?? string.Empty
        };
    }

    public async Task<string?> GetDocumentChunkSummaryAsync(
        string documentTitle,
        string? searchQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.IsConfigured || excerpts.Count == 0)
            return null;

        const string systemPrompt = """
            You summarize biomedical document excerpts for researchers.
            Write exactly one paragraph of approximately 100-120 words (do not exceed 130 words).
            Base the summary only on the provided excerpts. Use clear, fluent prose.
            Do not use markdown, headings, bullet points, numbered lists, or quoted raw excerpt text.
            When a user search query is provided, emphasize how the document content relates to that query.
            """;

        var userContent = BuildChunkSummaryPrompt(documentTitle, searchQuery, excerpts, maxCharsPerExcerpt: 1200);
        var summary = await SendCompletionAsync(
            systemPrompt,
            userContent,
            temperature: 0.3,
            maxTokens: 350,
            useJsonFormat: false,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(summary))
            return summary;

        _logger.LogInformation("Document chunk summary failed with full excerpts; retrying with shorter excerpts");
        var shorterPrompt = BuildChunkSummaryPrompt(documentTitle, searchQuery, excerpts, maxCharsPerExcerpt: 600);
        summary = await SendCompletionAsync(
            systemPrompt,
            shorterPrompt,
            temperature: 0.3,
            maxTokens: 350,
            useJsonFormat: false,
            cancellationToken).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(summary))
            return summary;

        var featuredQuery = string.IsNullOrWhiteSpace(searchQuery)
            ? $"Summarize the document titled \"{documentTitle}\" in one paragraph of approximately 100-120 words using only the provided excerpts."
            : $"""
               The user searched for "{searchQuery}".
               Summarize how the document titled "{documentTitle}" relates to this search in one paragraph of approximately 100-120 words using only the provided excerpts.
               """;

        return await GetFeaturedAnswerAsync(featuredQuery, excerpts, insufficientHighRelevanceExcerpts: false, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildChunkSummaryPrompt(
        string documentTitle,
        string? searchQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        int maxCharsPerExcerpt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Document title:").AppendLine(documentTitle).AppendLine();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            sb.AppendLine("User search query:").AppendLine(searchQuery.Trim()).AppendLine();
        }

        sb.AppendLine($"Summarize the following {excerpts.Count} excerpt(s) in approximately 100-120 words:");
        sb.AppendLine();
        for (var i = 0; i < excerpts.Count; i++)
        {
            var e = excerpts[i];
            var text = e.Text.Length > maxCharsPerExcerpt ? e.Text[..maxCharsPerExcerpt] + "..." : e.Text;
            sb.AppendLine($"Excerpt {i + 1}:");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task<string?> SendCompletionAsync(
        string systemPrompt,
        string userContent,
        double temperature,
        int maxTokens,
        bool useJsonFormat,
        CancellationToken cancellationToken)
    {
        var payload = new ChatCompletionRequest
        {
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userContent }
            ],
            Temperature = temperature,
            MaxTokens = maxTokens,
            ResponseFormat = useJsonFormat ? new ResponseFormat { Type = "json_object" } : null
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.CompletionApiUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("api-key", _settings.CompletionApiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, RequestJsonOptions),
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

            var parsed = JsonSerializer.Deserialize<ChatCompletionResponse>(body, ResponseJsonOptions);

            var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Completion API call failed");
            return null;
        }
    }

    private static string BuildAdcSystemPrompt()
    {
        return """
            You analyze biomedical document excerpts and extract antibody-drug conjugate (ADC) field values when present.
            Return ONLY valid JSON with exactly these keys: summary, adcName, antibodyName, payloadName, linkerName.

            summary: Write one cohesive paragraph of approximately 100-120 words (do not exceed 130 words) synthesizing ALL provided document excerpts/chunks.
            When a user search query is provided, frame the summary in that context and emphasize passages most relevant to the query.
            Cover the main topics, methods, findings, and conclusions present in the text.
            Always summarize what the excerpts actually discuss in clear prose—never paste or quote raw excerpt text, bullet lists, or numbered chunks.
            Never respond with phrases like "no ADC information found", "not available", or "could not be determined" in the summary.

            adcName, antibodyName, payloadName, linkerName: extract exact names/values only when explicitly present in the excerpts; use empty string when not found.
            Do not invent ADC field values. Base all output only on the provided excerpts.
            """;
    }

    private static string BuildAdcExtractionPrompt(
        string documentTitle,
        string? searchQuery,
        IReadOnlyList<SearchExcerpt> excerpts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Document title:").AppendLine(documentTitle).AppendLine();

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            sb.AppendLine("User search query (summarize in this context):").AppendLine(searchQuery.Trim()).AppendLine();
        }

        sb.AppendLine($"Below are the top {excerpts.Count} text chunk(s) from this document.");
        sb.AppendLine("Write a summary of approximately 100-120 words synthesizing all chunks in the context of the search query when provided.");
        sb.AppendLine("Then extract ADC Name, antibody name, payload name, and linker name if explicitly mentioned.");
        sb.AppendLine();
        for (var i = 0; i < excerpts.Count; i++)
        {
            var e = excerpts[i];
            var text = e.Text.Length > 2000 ? e.Text[..2000] + "..." : e.Text;
            sb.AppendLine($"Chunk {i + 1}:");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static DocumentAdcInfoResponse? ParseAdcInfoResponse(string content)
    {
        var json = ExtractJsonObject(content);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new DocumentAdcInfoResponse
            {
                Summary = GetJsonString(root, "summary"),
                AdcName = GetJsonString(root, "adcName"),
                AntibodyName = GetJsonString(root, "antibodyName"),
                PayloadName = GetJsonString(root, "payloadName"),
                LinkerName = GetJsonString(root, "linkerName")
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetJsonString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var prop))
            return string.Empty;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            _ => prop.ToString().Trim()
        };
    }

    private static string ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                return trimmed[start..(end + 1)];
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
            return trimmed[firstBrace..(lastBrace + 1)];

        return trimmed;
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

        sb.AppendLine("Search excerpts (relevance % is relative to the top-ranked result):");
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

        [JsonPropertyName("response_format")]
        public ResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class ResponseFormat
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "json_object";
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
