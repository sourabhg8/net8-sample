namespace Microservices.Configuration;

/// <summary>
/// Settings for user preferred (saved) search terms.
/// </summary>
public class PreferredSearchSettings
{
    public const string SectionName = "PreferredSearch";

    /// <summary>
    /// Maximum number of search terms stored per user (default 10).
    /// </summary>
    public int AllowedSearchTermsToStore { get; set; } = 10;
}
