namespace Microservices.Core.Entities;

/// <summary>
/// Represents a searchable item in the system
/// </summary>
public class SearchableItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // Full text content for searching
    public string Type { get; set; } = string.Empty; // document, user, organization, article, etc.
    public string Category { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

