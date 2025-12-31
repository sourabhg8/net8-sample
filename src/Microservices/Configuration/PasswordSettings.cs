namespace Microservices.Configuration;

/// <summary>
/// Password hashing configuration settings
/// </summary>
public class PasswordSettings
{
    public const string SectionName = "PasswordSettings";

    /// <summary>
    /// Secret key used for HMAC-based password hashing
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Number of iterations for key derivation (PBKDF2)
    /// </summary>
    public int Iterations { get; set; } = 100000;

    /// <summary>
    /// Salt size in bytes
    /// </summary>
    public int SaltSize { get; set; } = 16;

    /// <summary>
    /// Hash size in bytes
    /// </summary>
    public int HashSize { get; set; } = 32;
}

