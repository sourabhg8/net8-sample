using Microservices.Core.DTOs;
using Microservices.Core.Exceptions;
using Microservices.Core.Interfaces;
using Microservices.Configuration;
using Microsoft.Extensions.Options;

namespace Microservices.Infrastructure.Services;

/// <summary>
/// Authentication service implementation
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IJwtService _jwtService;
    private readonly IPasswordService _passwordService;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IJwtService jwtService,
        IPasswordService passwordService,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _jwtService = jwtService;
        _passwordService = passwordService;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Login attempt for user: {Username}", request.Username);

        var user = await _userRepository.GetByEmailAsync(request.Username, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login failed - user not found: {Username}", request.Username);
            throw new UnauthorizedException("Invalid username or password");
        }

        if (user.Status != "active")
        {
            _logger.LogWarning("Login failed - user is inactive: {Username}", request.Username);
            throw new UnauthorizedException("User account is disabled");
        }

        // Check organization status (skip for platform admin users)
        if (user.OrgId != "PLATFORM" && user.UserType != "platform_admin")
        {
            var organization = await _organizationRepository.GetByIdAsync(user.OrgId, cancellationToken);
            
            if (organization == null)
            {
                _logger.LogWarning("Login failed - organization not found for user: {Username}, OrgId: {OrgId}", 
                    request.Username, user.OrgId);
                throw new UnauthorizedException("Your organization account has been removed");
            }

            if (organization.Status == "suspended")
            {
                _logger.LogWarning("Login failed - organization suspended for user: {Username}, OrgId: {OrgId}", 
                    request.Username, user.OrgId);
                throw new UnauthorizedException("Your organisation subscription is suspended. Please contact support.");
            }

            if (organization.Status == "cancelled")
            {
                _logger.LogWarning("Login failed - organization cancelled for user: {Username}, OrgId: {OrgId}", 
                    request.Username, user.OrgId);
                throw new UnauthorizedException("Your organisation subscription has been cancelled. Please contact support.");
            }
        }

        // Verify password using password service
        if (!_passwordService.VerifyPassword(request.Password, user.Auth.PasswordHash))
        {
            _logger.LogWarning("Login failed - invalid password for user: {Username}", request.Username);
            throw new UnauthorizedException("Invalid username or password");
        }

        var token = _jwtService.GenerateToken(user);

        _logger.LogInformation("Login successful for user: {Username}", request.Username);

        return new LoginResponse
        {
            Token = token,
            TokenType = "Bearer",
            ExpiresIn = _jwtSettings.ExpirationMinutes * 60, // Convert to seconds
            User = new UserInfo
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                UserType = user.UserType,
                Role = user.Role,
                OrgId = user.OrgId,
                OrgName = user.OrgName
            }
        };
    }

    public async Task<bool> ValidateUserAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByEmailAsync(username, cancellationToken);
        
        if (user == null || user.Status != "active")
            return false;

        // Check organization status (skip for platform admin users)
        if (user.OrgId != "PLATFORM" && user.UserType != "platform_admin")
        {
            var organization = await _organizationRepository.GetByIdAsync(user.OrgId, cancellationToken);
            
            if (organization == null || organization.Status != "active")
                return false;
        }

        // Verify password using password service
        return _passwordService.VerifyPassword(password, user.Auth.PasswordHash);
    }
}
