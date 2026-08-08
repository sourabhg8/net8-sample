using System.Text.Json.Serialization;

namespace Microservices.Core.Entities;

/// <summary>
/// One document per user in the preferredSearches Cosmos container.
/// </summary>
public class PreferredSearchDocument
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;

    [JsonPropertyName("searchTerms")]
    public List<SavedSearchTerm> SearchTerms { get; set; } = new();

    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public class SavedSearchTerm
{
    [JsonPropertyName("searchTerm")]
    public string SearchTerm { get; set; } = string.Empty;

    [JsonPropertyName("searchTermSavedAt")]
    public DateTime SearchTermSavedAt { get; set; }

    [JsonPropertyName("searchTermLastSearchedAt")]
    public DateTime SearchTermLastSearchedAt { get; set; }
}
