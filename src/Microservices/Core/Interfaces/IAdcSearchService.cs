using Microservices.Core.DTOs;

namespace Microservices.Core.Interfaces;

public interface IAdcSearchService
{
    Task<AdcSearchResponse> SearchAsync(AdcSearchRequest request, CancellationToken cancellationToken = default);
}
