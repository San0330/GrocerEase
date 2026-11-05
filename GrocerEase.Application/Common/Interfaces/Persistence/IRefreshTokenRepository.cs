using GrocerEase.Domain.Entities;

namespace GrocerEase.Application.Common.Interfaces.Persistence;

public interface IRefreshTokenRepository
{
    /// <summary>Finds a token by the hash of what the caller presented. The returned token is tracked.</summary>
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>Every token that descends from one login, revoked or not. The returned tokens are tracked.</summary>
    Task<IReadOnlyList<RefreshToken>> GetFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);
}
