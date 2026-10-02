namespace Microservices.Configuration;

/// <summary>
/// Relevance display scaling for ADC hybrid search (@search.score tiers differ from research papers).
/// </summary>
public class AdcSearchRelevanceSettings
{
    public const string SectionName = "AdcSearchRelevance";

    public double RelevanceDisplayMaxPercent { get; set; } = 90;

    public double? RelevanceScoreAnchor { get; set; }

    public List<RelevanceScoreTier> RelevanceScoreTiers { get; set; } = new()
    {
        new RelevanceScoreTier { MinRawScore = 0.032, DisplayPercent = 90 },
        new RelevanceScoreTier { MinRawScore = 0.02, DisplayPercent = 70 },
        new RelevanceScoreTier { MinRawScore = 0, DisplayPercent = 50 }
    };

    /// <summary>Records validated within this many days show the "New" badge in the UI.</summary>
    public int NewBadgeWithinDays { get; set; } = 90;
}
