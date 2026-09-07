using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _db;

    public RefreshTokenRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);
    }

    public async Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.RefreshTokens.FindAsync(new object[] { id }, ct);
    }

    public async Task CreateAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        await _db.RefreshTokens.AddAsync(refreshToken, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens.FindAsync(new object[] { id }, ct);
        if (token != null)
        {
            token.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> CleanupExpiredTokens(CancellationToken ct = default)
    {
        var expiredTokens = await _db.RefreshTokens
            .Where(r => r.ExpiresAt < DateTime.UtcNow || r.RevokedAt != null)
            .ToListAsync(ct);

        _db.RefreshTokens.RemoveRange(expiredTokens);
        await _db.SaveChangesAsync(ct);

        return expiredTokens.Count;
    }
}
