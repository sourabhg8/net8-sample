namespace Microservices.Core.DTOs;

/// <summary>
/// Response model for successful login
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public UserInfo User { get; set; } = new();
}

/// <summary>
/// Basic user information returned after login
/// </summary>
public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string UserType { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string OrgId { get; set; } = string.Empty;
    public string OrgName { get; set; } = string.Empty;
}
