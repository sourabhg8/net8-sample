using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microservices.Core.DTOs;
using Microservices.Core.Interfaces;
using Microservices.Middleware;

namespace Microservices.Controllers;

/// <summary>
/// Controller for search operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly ISearchService _searchService;
    private readonly ILogger<SearchController> _logger;

    public SearchController(ISearchService searchService, ILogger<SearchController> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Search for items matching the query
    /// </summary>
    /// <param name="request">Search request with query and pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results with pagination</returns>
    [HttpPost]
    //[Authorize]
    [ProducesResponseType(typeof(ApiResponse<SearchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SearchResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SearchResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SearchResponse>>> Search(
        [FromBody] SearchRequest request,
        CancellationToken cancellationToken)
    {
        var correlationId = HttpContext.GetCorrelationId();

        _logger.LogInformation(
            "Search request: Query='{Query}', Page={Page}, CorrelationId={CorrelationId}",
            request.SearchQuery, request.PageNumber, correlationId);

        var response = await _searchService.SearchAsync(request, cancellationToken);

        return Ok(ApiResponse<SearchResponse>.SuccessResponse(
            response,
            $"Found {response.TotalResults} results",
            correlationId));
    }

    /// <summary>
    /// Quick search with minimal parameters (GET endpoint for simple searches)
    /// </summary>
    /// <param name="q">Search query</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Results per page (default: 10)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Search results</returns>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SearchResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<SearchResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<SearchResponse>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<SearchResponse>>> QuickSearch(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var correlationId = HttpContext.GetCorrelationId();

        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(ApiResponse<SearchResponse>.FailureResponse(
                "Search query is required",
                null,
                correlationId));
        }

        var request = new SearchRequest
        {
            SearchQuery = q,
            PageNumber = page,
            PageSize = pageSize
        };

        var response = await _searchService.SearchAsync(request, cancellationToken);

        return Ok(ApiResponse<SearchResponse>.SuccessResponse(
            response,
            $"Found {response.TotalResults} results",
            correlationId));
    }
}

