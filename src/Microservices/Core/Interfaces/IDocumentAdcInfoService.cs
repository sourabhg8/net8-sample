using Microservices.Core.DTOs;

namespace Microservices.Core.Interfaces;

public interface IDocumentAdcInfoService
{
    Task<DocumentAdcInfoResponse> GetDocumentAdcInfoAsync(
        string documentTitle,
        string? searchQuery = null,
        CancellationToken cancellationToken = default);
}
