using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microservices.Core.DTOs;
using Microservices.Core.Exceptions;
using Microservices.Middleware;

namespace Microservices.Controllers;

/// <summary>
/// Controller demonstrating role-based authorization
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class ProtectedController : ControllerBase
{
    private readonly ILogger<ProtectedController> _logger;

    public ProtectedController(ILogger<ProtectedController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Endpoint accessible by any authenticated user
    /// </summary>
    [HttpGet("public-data")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<object>> GetPublicData()
    {
        var correlationId = HttpContext.GetCorrelationId();
        _logger.LogInformation("Public data accessed by user: {User}", User.Identity?.Name);

        var data = new
        {
            Message = "This data is accessible to any authenticated user",
            AccessedAt = DateTime.UtcNow,
            UserRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            data,
            "Public data retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Endpoint accessible only by Admin users
    /// </summary>
    [HttpGet("admin-only")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public ActionResult<ApiResponse<object>> GetAdminData()
    {
        var correlationId = HttpContext.GetCorrelationId();
        _logger.LogInformation("Admin data accessed by user: {User}", User.Identity?.Name);

        var data = new
        {
            Message = "This data is only accessible to Admin users",
            SecretInfo = "Admin-only sensitive information",
            AccessedAt = DateTime.UtcNow
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            data,
            "Admin data retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Endpoint accessible by Admin or Manager users
    /// </summary>
    [HttpGet("manager-data")]
    [Authorize(Roles = "Admin,Manager")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public ActionResult<ApiResponse<object>> GetManagerData()
    {
        var correlationId = HttpContext.GetCorrelationId();
        _logger.LogInformation("Manager data accessed by user: {User}", User.Identity?.Name);

        var data = new
        {
            Message = "This data is accessible to Admin and Manager users",
            Reports = new[]
            {
                new { Name = "Sales Report", Status = "Generated" },
                new { Name = "Performance Report", Status = "Pending" }
            },
            AccessedAt = DateTime.UtcNow
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            data,
            "Manager data retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Endpoint that checks organisation tier from claims
    /// </summary>
    [HttpGet("enterprise-feature")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public ActionResult<ApiResponse<object>> GetEnterpriseFeature()
    {
        var correlationId = HttpContext.GetCorrelationId();
        var organisationTier = User.FindFirst("organisation_tier")?.Value;

        if (organisationTier != "Enterprise")
        {
            _logger.LogWarning("User attempted to access enterprise feature without proper tier. Current tier: {Tier}", organisationTier);
            throw new ForbiddenException("This feature is only available for Enterprise tier organisations");
        }

        var data = new
        {
            Message = "Enterprise feature accessed successfully",
            Features = new[]
            {
                "Advanced Analytics",
                "Priority Support",
                "Custom Integrations",
                "Unlimited API Calls"
            },
            OrganisationTier = organisationTier,
            AccessedAt = DateTime.UtcNow
        };

        return Ok(ApiResponse<object>.SuccessResponse(
            data,
            "Enterprise feature data retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Test endpoint to demonstrate exception handling
    /// </summary>
    [HttpGet("test-error/{errorType}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public ActionResult<ApiResponse<object>> TestError(string errorType)
    {
        var correlationId = HttpContext.GetCorrelationId();
        _logger.LogInformation("Test error endpoint called with type: {ErrorType}", errorType);

        switch (errorType.ToLower())
        {
            case "notfound":
                throw new NotFoundException("Resource", "test-id");
            case "validation":
                throw new ValidationException(new[] { "Field1 is required", "Field2 must be positive" });
            case "business":
                throw new BusinessException("Business rule violation: Cannot process this request");
            case "conflict":
                throw new ConflictException("Resource", "Already exists with this identifier");
            case "forbidden":
                throw new ForbiddenException("You don't have permission to perform this action");
            case "unauthorized":
                throw new UnauthorizedException("Authentication required");
            case "internal":
                throw new InvalidOperationException("Simulated internal server error");
            default:
                return Ok(ApiResponse<object>.SuccessResponse(
                    new { Message = "No error triggered" },
                    "Test completed successfully",
                    correlationId));
        }
    }
}
