using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microservices.Core.DTOs;
using Microservices.Core.Interfaces;
using Microservices.Middleware;

namespace Microservices.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PreferredSearchController : ControllerBase
{
    private readonly IPreferredSearchService _preferredSearchService;
    private readonly ILogger<PreferredSearchController> _logger;

    public PreferredSearchController(
        IPreferredSearchService preferredSearchService,
        ILogger<PreferredSearchController> logger)
    {
        _preferredSearchService = preferredSearchService;
        _logger = logger;
    }

    /// <summary>
    /// Gets saved search terms for the current user (ordered by searchTermLastSearchedAt desc).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PreferredSearchListResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PreferredSearchListResponse>>> Get(CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var userId = GetCurrentUserId();

        var terms = await _preferredSearchService.GetSearchTermsAsync(userId, cancellationToken);

        return Ok(ApiResponse<PreferredSearchListResponse>.SuccessResponse(
            new PreferredSearchListResponse { SearchTerms = terms.ToList() },
            "Preferred searches retrieved successfully",
            correlationId));
    }

    /// <summary>
    /// Saves a search term for the current user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<PreferredSearchListResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<PreferredSearchListResponse>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PreferredSearchListResponse>>> Save(
        [FromBody] SavePreferredSearchRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var userId = GetCurrentUserId();

        _logger.LogInformation("Saving preferred search for user {UserId}", userId);

        var terms = await _preferredSearchService.SaveSearchTermAsync(userId, request.SearchTerm, cancellationToken);

        return Ok(ApiResponse<PreferredSearchListResponse>.SuccessResponse(
            new PreferredSearchListResponse { SearchTerms = terms.ToList() },
            "Search term saved successfully",
            correlationId));
    }

    /// <summary>
    /// Updates searchTermLastSearchedAt when the user runs a saved search.
    /// </summary>
    [HttpPost("record")]
    [ProducesResponseType(typeof(ApiResponse<PreferredSearchListResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PreferredSearchListResponse>>> Record(
        [FromBody] SavePreferredSearchRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();
        var userId = GetCurrentUserId();

        var terms = await _preferredSearchService.RecordSearchTermAsync(userId, request.SearchTerm, cancellationToken);

        return Ok(ApiResponse<PreferredSearchListResponse>.SuccessResponse(
            new PreferredSearchListResponse { SearchTerms = terms.ToList() },
            "Search term activity recorded",
            correlationId));
    }

    private string GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? throw new UnauthorizedAccessException("User id not found in token.");
    }
}
