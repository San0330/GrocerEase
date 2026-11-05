using GrocerEase.Application.Common.Interfaces.Persistence;
using GrocerEase.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GrocerEase.Infrastructure.Persistence;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> GetFamilyAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        return await _context.RefreshTokens.Where(t => t.FamilyId == familyId).ToListAsync(cancellationToken);
    }

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _context.RefreshTokens.Add(token);
        return Task.CompletedTask;
    }
}
