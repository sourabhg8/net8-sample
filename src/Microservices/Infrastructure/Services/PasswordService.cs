using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microservices.Configuration;
using Microservices.Core.Interfaces;

namespace Microservices.Infrastructure.Services;

/// <summary>
/// Password hashing service using PBKDF2 with HMAC-SHA256
/// </summary>
public class PasswordService : IPasswordService
{
    private readonly PasswordSettings _settings;
    private readonly ILogger<PasswordService> _logger;

    public PasswordService(
        IOptions<PasswordSettings> settings,
        ILogger<PasswordService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Hash a password using PBKDF2 with a random salt and secret key
    /// Format: {iterations}.{salt_base64}.{hash_base64}
    /// </summary>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be null or empty", nameof(password));
        }

        // Generate a random salt
        var salt = RandomNumberGenerator.GetBytes(_settings.SaltSize);

        // Combine password with secret key for additional security
        var passwordWithKey = password + _settings.SecretKey;

        // Derive the hash using PBKDF2 with SHA256
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(passwordWithKey),
            salt,
            _settings.Iterations,
            HashAlgorithmName.SHA256,
            _settings.HashSize);

        // Format: iterations.salt.hash (all base64 encoded where applicable)
        var hashedPassword = $"{_settings.Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";

        _logger.LogDebug("Password hashed successfully");

        return hashedPassword;
    }

    /// <summary>
    /// Verify a password against a stored hash
    /// </summary>
    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        try
        {
            // Parse the stored hash
            var parts = hashedPassword.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("Invalid hash format - expected 3 parts, got {Parts}", parts.Length);
                return false;
            }

            var iterations = int.Parse(parts[0]);
            var salt = Convert.FromBase64String(parts[1]);
            var storedHash = Convert.FromBase64String(parts[2]);

            // Combine password with secret key
            var passwordWithKey = password + _settings.SecretKey;

            // Derive hash using same parameters
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(passwordWithKey),
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                storedHash.Length);

            // Use constant-time comparison to prevent timing attacks
            var isValid = CryptographicOperations.FixedTimeEquals(computedHash, storedHash);

            _logger.LogDebug("Password verification completed: {Result}", isValid ? "Valid" : "Invalid");

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying password");
            return false;
        }
    }
}

