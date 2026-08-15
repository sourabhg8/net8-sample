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

        var result = await ExtractAdcFieldsInternalAsync(documentTitle, searchQuery, excerpts, strictMode: false, cancellationToken)
            .ConfigureAwait(false);

        if (result != null && HasAnyAdcField(result))
            return result;

        _logger.LogInformation(
            "ADC field extraction returned no values for '{Title}'; retrying with focused extraction prompt",
            documentTitle);

        var retry = await ExtractAdcFieldsInternalAsync(documentTitle, searchQuery, excerpts, strictMode: true, cancellationToken)
            .ConfigureAwait(false);

        return retry ?? result;
    }

    private async Task<DocumentAdcInfoResponse?> ExtractAdcFieldsInternalAsync(
        string documentTitle,
        string? searchQuery,
        IReadOnlyList<SearchExcerpt> excerpts,
        bool strictMode,
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildAdcFieldsSystemPrompt(strictMode);
        var userContent = BuildAdcFieldsExtractionPrompt(documentTitle, excerpts);

        var text = await SendCompletionAsync(
            systemPrompt,
            userContent,
            temperature: 0.1,
            maxTokens: 500,
            useJsonFormat: true,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogInformation("ADC fields JSON completion failed; retrying without response_format");
            text = await SendCompletionAsync(
                systemPrompt,
                userContent,
                temperature: 0.1,
                maxTokens: 500,
                useJsonFormat: false,
                cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(text))
            return null;

        var parsed = ParseAdcFieldsResponse(text);
        if (parsed != null)
            return parsed;

        _logger.LogWarning(
            "Failed to parse ADC fields from completion response for '{Title}'. Response preview: {Preview}",
            documentTitle,
            text.Length > 300 ? text[..300] + "..." : text);

        return null;
    }

    private static bool HasAnyAdcField(DocumentAdcInfoResponse response) =>
        !string.IsNullOrWhiteSpace(response.AdcName)
        || !string.IsNullOrWhiteSpace(response.AntibodyName)
        || !string.IsNullOrWhiteSpace(response.PayloadName)
        || !string.IsNullOrWhiteSpace(response.LinkerName);

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

    private static string BuildAdcFieldsSystemPrompt(bool strictMode)
    {
        var strictHint = strictMode
            ? """
              Search every excerpt carefully. Look for abbreviations, trade names, generic names, and parenthetical mentions.
              Common patterns: "ADC", "antibody-drug conjugate", "mAb", "cytotoxic payload", "linker", "warhead", "conjugate".
              """
            : string.Empty;

        return """
            You extract antibody-drug conjugate (ADC) metadata from biomedical document excerpts.
            Return ONLY valid JSON with exactly these four string keys: adcName, antibodyName, payloadName, linkerName.
            Do not include any other keys.

            Field definitions:
            - adcName: the ADC compound or product name (e.g. trastuzumab deruxtecan, T-DXd, Adcetris, brentuximab vedotin)
            - antibodyName: the targeting antibody or mAb (e.g. trastuzumab, cetuximab, hRS7)
            - payloadName: the cytotoxic drug/payload (e.g. MMAE, DM1, deruxtecan, SN-38, exatecan)
            - linkerName: the linker chemistry or name (e.g. VC-PAB, SMCC, cleavable linker, GGFG)

            """
            + strictHint
            + """
            Extract values explicitly stated or clearly implied in the excerpts. Use empty string only when a field is truly absent.
            Do not invent values. Example output:
            {"adcName":"trastuzumab deruxtecan","antibodyName":"trastuzumab","payloadName":"deruxtecan","linkerName":"tetrapeptide-based cleavable linker"}
            """;
    }

    private static string BuildAdcFieldsExtractionPrompt(
        string documentTitle,
        IReadOnlyList<SearchExcerpt> excerpts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Document title:").AppendLine(documentTitle).AppendLine();
        sb.AppendLine("Extract adcName, antibodyName, payloadName, and linkerName from these excerpts.");
        sb.AppendLine("Return JSON with keys: adcName, antibodyName, payloadName, linkerName.");
        sb.AppendLine();
        for (var i = 0; i < excerpts.Count; i++)
        {
            var e = excerpts[i];
            var text = e.Text.Length > 2000 ? e.Text[..2000] + "..." : e.Text;
            sb.AppendLine($"Excerpt {i + 1}:");
            sb.AppendLine(text);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static DocumentAdcInfoResponse? ParseAdcFieldsResponse(string content)
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
                AdcName = GetFlexibleJsonString(root, "adcName", "adc_name", "ADCName", "ADC Name", "adc", "compoundName", "compound_name"),
                AntibodyName = GetFlexibleJsonString(root, "antibodyName", "antibody_name", "AntibodyName", "Antibody Name", "antibody", "mAb", "monoclonalAntibody"),
                PayloadName = GetFlexibleJsonString(root, "payloadName", "payload_name", "PayloadName", "Payload Name", "payload", "cytotoxicPayload", "drugPayload", "warhead"),
                LinkerName = GetFlexibleJsonString(root, "linkerName", "linker_name", "LinkerName", "Linker Name", "linker", "linkerChemistry")
            };
        }
        catch
        {
            return null;
        }
    }

    private static string GetFlexibleJsonString(JsonElement root, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            var value = TryGetJsonPropertyString(root, name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        var normalizedTargets = propertyNames.Select(NormalizeJsonKey).ToHashSet(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!normalizedTargets.Contains(NormalizeJsonKey(property.Name)))
                continue;

            var value = GetJsonElementString(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string? TryGetJsonPropertyString(JsonElement root, string propertyName)
    {
        if (root.TryGetProperty(propertyName, out var direct))
            return NullIfEmpty(GetJsonElementString(direct));

        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            return NullIfEmpty(GetJsonElementString(property.Value));
        }

        return null;
    }

    private static string GetJsonElementString(JsonElement prop) =>
        prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString()?.Trim() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => prop.ToString().Trim(),
            _ => prop.ToString().Trim()
        };

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string NormalizeJsonKey(string key) =>
        key.Replace(" ", string.Empty, StringComparison.Ordinal)
           .Replace("_", string.Empty, StringComparison.Ordinal)
           .Replace("-", string.Empty, StringComparison.Ordinal)
           .ToLowerInvariant();

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
