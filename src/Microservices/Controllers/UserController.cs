using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microservices.Core.DTOs;
using Microservices.Core.Interfaces;
using Microservices.Middleware;
using System.Security.Claims;

namespace Microservices.Controllers;

/// <summary>
/// Controller for managing users (Platform Admin and Org Admin)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = "platform_admin,org_admin")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(
        IUserService userService,
        ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all users (Platform Admin only) or users from specific org
    /// </summary>
    /// <param name="orgId">Organization ID (required for org admins, optional for platform admins)</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of users</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<UserListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserListResponse>>> GetAll(
        [FromQuery] string? orgId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var (currentUserId, currentUserOrgId, isPlatformAdmin) = GetCurrentUserInfo();

        _logger.LogInformation("Getting users - OrgId: {OrgId}, Page: {Page}, PageSize: {PageSize}, RequestedBy: {UserId}", 
            orgId, page, pageSize, currentUserId);

        // Validate pagination parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        UserListResponse result;

        if (isPlatformAdmin)
        {
            // Platform admin can see all users or filter by org
            if (!string.IsNullOrEmpty(orgId))
            {
                result = await _userService.GetByOrgIdAsync(orgId, page, pageSize, cancellationToken);
            }
            else
            {
                result = await _userService.GetAllAsync(page, pageSize, cancellationToken);
            }
        }
        else
        {
            // Org admin can only see users from their organization
            result = await _userService.GetByOrgIdAsync(currentUserOrgId!, page, pageSize, cancellationToken);
        }

        return Ok(ApiResponse<UserListResponse>.SuccessResponse(
            result,
            "Users retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Gets a user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>User details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> GetById(
        string id,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var (currentUserId, currentUserOrgId, isPlatformAdmin) = GetCurrentUserInfo();

        _logger.LogInformation("Getting user by ID: {UserId}, RequestedBy: {RequesterId}", id, currentUserId);

        var result = await _userService.GetByIdAsync(id, currentUserOrgId, isPlatformAdmin, cancellationToken);

        return Ok(ApiResponse<UserResponse>.SuccessResponse(
            result,
            "User retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Creates a new user in an organization
    /// </summary>
    /// <param name="orgId">Organization ID to create user in</param>
    /// <param name="orgName">Organization Name</param>
    /// <param name="request">User creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created user</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Create(
        [FromQuery] string orgId,
        [FromQuery] string orgName,
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var (currentUserId, currentUserOrgId, isPlatformAdmin) = GetCurrentUserInfo();
        var currentUserOrgName = GetCurrentUserOrgName();

        _logger.LogInformation("Creating user: {Email} in org: {OrgId} by user: {UserId}", 
            request.Email, orgId, currentUserId);

        // Determine which org to create user in
        string targetOrgId;
        string targetOrgName;

        if (isPlatformAdmin)
        {
            // Platform admin can create user in any org
            targetOrgId = orgId;
            targetOrgName = orgName;
        }
        else
        {
            // Org admin can only create users in their own org
            if (!string.IsNullOrEmpty(orgId) && orgId != currentUserOrgId)
            {
                return Forbid();
            }
            targetOrgId = currentUserOrgId!;
            targetOrgName = currentUserOrgName!;
        }

        var result = await _userService.CreateAsync(request, targetOrgId, targetOrgName, currentUserId, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<UserResponse>.SuccessResponse(
                result,
                "User created successfully",
                correlationId));
    }

    /// <summary>
    /// Updates an existing user
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="request">User update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated user</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Update(
        string id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var (currentUserId, currentUserOrgId, isPlatformAdmin) = GetCurrentUserInfo();

        _logger.LogInformation("Updating user: {UserId} by user: {RequesterId}", id, currentUserId);

        var result = await _userService.UpdateAsync(id, request, currentUserOrgId, isPlatformAdmin, currentUserId, cancellationToken);

        return Ok(ApiResponse<UserResponse>.SuccessResponse(
            result,
            "User updated successfully",
            correlationId));
    }

    /// <summary>
    /// Deletes a user (soft delete)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse>> Delete(
        string id,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var (currentUserId, currentUserOrgId, isPlatformAdmin) = GetCurrentUserInfo();

        _logger.LogInformation("Deleting user: {UserId} by user: {RequesterId}", id, currentUserId);

        await _userService.DeleteAsync(id, currentUserOrgId, isPlatformAdmin, currentUserId, cancellationToken);

        return Ok(ApiResponse.SuccessResponse(
            "User deleted successfully",
            correlationId));
    }

    /// <summary>
    /// Resets a user's password to auto-generated password
    /// Password format: first 4 letters of email + "_" + first 4 letters of name (lowercase)
    /// </summary>
    /// <param name="id">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse>> ResetPassword(
        string id,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var (currentUserId, currentUserOrgId, isPlatformAdmin) = GetCurrentUserInfo();

        _logger.LogInformation("Resetting password for user: {UserId} by user: {RequesterId}", id, currentUserId);

        await _userService.ResetPasswordAsync(id, currentUserOrgId, isPlatformAdmin, currentUserId, cancellationToken);

        return Ok(ApiResponse.SuccessResponse(
            "Password reset successfully. New password: first 4 letters of email + '_' + first 4 letters of name (lowercase)",
            correlationId));
    }

    private (string userId, string? orgId, bool isPlatformAdmin) GetCurrentUserInfo()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value 
            ?? "unknown";
        
        var orgId = User.FindFirst("organisation_id")?.Value;
        
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        var isPlatformAdmin = role?.Equals("platform_admin", StringComparison.OrdinalIgnoreCase) == true;

        return (userId, orgId, isPlatformAdmin);
    }

    private string? GetCurrentUserOrgName()
    {
        return User.FindFirst("organisation_name")?.Value;
    }
}

