using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microservices.Core.DTOs;
using Microservices.Core.Interfaces;
using Microservices.Middleware;
using System.Security.Claims;

namespace Microservices.Controllers;

/// <summary>
/// Controller for managing organizations (Platform Admin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Roles = "platform_admin")]
public class OrganizationController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly ILogger<OrganizationController> _logger;

    public OrganizationController(
        IOrganizationService organizationService,
        ILogger<OrganizationController> logger)
    {
        _organizationService = organizationService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all organizations with pagination
    /// </summary>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 10, max: 100)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of organizations</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<OrganizationListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<OrganizationListResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        _logger.LogInformation("Getting all organizations - Page: {Page}, PageSize: {PageSize}", page, pageSize);

        // Validate pagination parameters
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _organizationService.GetAllAsync(page, pageSize, cancellationToken);

        return Ok(ApiResponse<OrganizationListResponse>.SuccessResponse(
            result,
            "Organizations retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Gets an organization by ID
    /// </summary>
    /// <param name="id">Organization ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Organization details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> GetById(
        string id,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        _logger.LogInformation("Getting organization by ID: {OrgId}", id);

        var result = await _organizationService.GetByIdAsync(id, cancellationToken);

        return Ok(ApiResponse<OrganizationResponse>.SuccessResponse(
            result,
            "Organization retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Creates a new organization
    /// </summary>
    /// <param name="request">Organization creation request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created organization</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<OrganizationResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var userId = GetCurrentUserId();
        
        _logger.LogInformation("Creating organization: {OrgName} by user: {UserId}", request.Name, userId);

        var result = await _organizationService.CreateAsync(request, userId, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<OrganizationResponse>.SuccessResponse(
                result,
                "Organization created successfully",
                correlationId));
    }

    /// <summary>
    /// Updates an existing organization
    /// </summary>
    /// <param name="id">Organization ID</param>
    /// <param name="request">Organization update request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated organization</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<OrganizationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ApiResponse<OrganizationResponse>>> Update(
        string id,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var userId = GetCurrentUserId();
        
        _logger.LogInformation("Updating organization: {OrgId} by user: {UserId}", id, userId);

        var result = await _organizationService.UpdateAsync(id, request, userId, cancellationToken);

        return Ok(ApiResponse<OrganizationResponse>.SuccessResponse(
            result,
            "Organization updated successfully",
            correlationId));
    }

    /// <summary>
    /// Deletes an organization (soft delete)
    /// </summary>
    /// <param name="id">Organization ID</param>
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
        var userId = GetCurrentUserId();
        
        _logger.LogInformation("Deleting organization: {OrgId} by user: {UserId}", id, userId);

        await _organizationService.DeleteAsync(id, userId, cancellationToken);

        return Ok(ApiResponse.SuccessResponse(
            "Organization deleted successfully",
            correlationId));
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst("sub")?.Value 
            ?? "unknown";
    }
}

