namespace Microservices.Configuration;

/// <summary>
/// General application settings
/// </summary>
public class AppSettings
{
    public const string SectionName = "AppSettings";

    public string ApplicationName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
}
