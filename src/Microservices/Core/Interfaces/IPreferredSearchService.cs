using Microservices.Core.DTOs;

namespace Microservices.Core.Interfaces;

public interface IPreferredSearchService
{
    Task<IReadOnlyList<PreferredSearchTermDto>> GetSearchTermsAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PreferredSearchTermDto>> SaveSearchTermAsync(string userId, string searchTerm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PreferredSearchTermDto>> RecordSearchTermAsync(string userId, string searchTerm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PreferredSearchTermDto>> DeleteSearchTermAsync(string userId, string searchTerm, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PreferredSearchTermDto>> DeleteAllSearchTermsAsync(string userId, CancellationToken cancellationToken = default);
}
