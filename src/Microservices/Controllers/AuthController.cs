using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microservices.Core.DTOs;
using Microservices.Core.Interfaces;
using Microservices.Middleware;
using System.Security.Claims;

namespace Microservices.Controllers;

/// <summary>
/// Authentication controller for login operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IUserService userService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JWT token and user information</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();
        _logger.LogInformation("Login request received for user: {Username}", request.Username);

        var result = await _authService.LoginAsync(request, cancellationToken);

        return Ok(ApiResponse<LoginResponse>.SuccessResponse(
            result,
            "Login successful",
            correlationId));
    }

    /// <summary>
    /// Test endpoint to verify token validation
    /// </summary>
    /// <returns>Current user information</returns>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<object>> GetCurrentUser()
    {
        var correlationId = HttpContext.GetCorrelationId();
        
        var userInfo = new
        {
            UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            Username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value 
                ?? User.FindFirst("unique_name")?.Value,
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value,
            Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
            UserType = User.FindFirst("user_type")?.Value,
            OrgId = User.FindFirst("organisation_id")?.Value,
            OrgName = User.FindFirst("organisation_name")?.Value
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            userInfo,
            "User information retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Change password for the authenticated user
    /// </summary>
    /// <param name="request">Change password request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        _logger.LogInformation("Password change request for user: {UserId}", userId);

        await _userService.ChangePasswordAsync(userId, request, cancellationToken);

        return Ok(ApiResponse.SuccessResponse(
            "Password changed successfully",
            correlationId));
    }
}
