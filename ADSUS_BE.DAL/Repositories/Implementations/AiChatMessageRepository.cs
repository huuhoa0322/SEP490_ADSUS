using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IAiChatMessageRepository.
/// Read-only queries dùng AsNoTracking. KHÔNG có RemoveAsync (GB-03).
/// </summary>
public sealed class AiChatMessageRepository : IAiChatMessageRepository
{
    private readonly AppDbContext _db;

    public AiChatMessageRepository(AppDbContext db) => _db = db;

    public async Task<AiChatMessage> AddAsync(AiChatMessage message, CancellationToken ct = default)
    {
        _db.AiChatMessages.Add(message);
        await _db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<IReadOnlyList<AiChatMessage>> ListByUserAsync(
        Guid userId,
        DateTime from,
        DateTime to,
        int limit,
        CancellationToken ct = default)
    {
        return await _db.AiChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.CreatedAt >= from && m.CreatedAt <= to)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
    }
}
