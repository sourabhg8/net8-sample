using Microservices.Core.Entities;

namespace Microservices.Core.Interfaces;

public interface IPreferredSearchRepository
{
    Task<PreferredSearchDocument?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<PreferredSearchDocument> UpsertAsync(PreferredSearchDocument document, CancellationToken cancellationToken = default);
}
