using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public class UserFcmTokenRepository : IUserFcmTokenRepository
{
    private readonly AppDbContext _db;

    public UserFcmTokenRepository(AppDbContext db) => _db = db;

    public async Task UpsertAsync(Guid userId, string token, string deviceType, CancellationToken ct = default)
    {
        var existing = await _db.UserFcmTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (existing is not null)
        {
            existing.UserId = userId;
            existing.DeviceType = deviceType;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newToken = new UserFcmToken
            {
                TokenId = Guid.NewGuid(),
                UserId = userId,
                Token = token,
                DeviceType = deviceType,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await _db.UserFcmTokens.AddAsync(newToken, ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteByTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenEntity = await _db.UserFcmTokens
            .FirstOrDefaultAsync(t => t.Token == token, ct);

        if (tokenEntity is not null)
        {
            _db.UserFcmTokens.Remove(tokenEntity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteAllByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        await _db.UserFcmTokens
            .Where(t => t.UserId == userId)
            .ExecuteDeleteAsync(ct);
    }
}
