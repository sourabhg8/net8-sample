namespace Microservices.Core.Interfaces;

/// <summary>
/// Service interface for password hashing and verification operations
/// </summary>
public interface IPasswordService
{
    /// <summary>
    /// Hash a plain text password
    /// </summary>
    /// <param name="password">The plain text password to hash</param>
    /// <returns>The hashed password string (includes salt)</returns>
    string HashPassword(string password);

    /// <summary>
    /// Verify a plain text password against a hashed password
    /// </summary>
    /// <param name="password">The plain text password to verify</param>
    /// <param name="hashedPassword">The hashed password to compare against</param>
    /// <returns>True if the password matches, false otherwise</returns>
    bool VerifyPassword(string password, string hashedPassword);
}

