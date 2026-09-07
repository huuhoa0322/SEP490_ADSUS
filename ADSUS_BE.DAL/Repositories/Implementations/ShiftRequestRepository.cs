using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public class ShiftRequestRepository : IShiftRequestRepository
{
    private readonly AppDbContext _context;

    public ShiftRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ShiftRequest?> GetByIdAsync(Guid requestId, CancellationToken ct = default)
    {
        return await _context.ShiftRequests
            .Include(r => r.User)
            .Include(r => r.ReviewedByNavigation)
            .FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
    }

    public async Task<(IReadOnlyList<ShiftRequest> Items, int Total)> ListAsync(
        Guid? userId, ShiftRequestStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.ShiftRequests
            .Include(r => r.User)
            .Include(r => r.ReviewedByNavigation)
            .AsQueryable();

        if (userId.HasValue)
            query = query.Where(r => r.UserId == userId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<bool> HasActiveRequestAsync(
        Guid userId, DateOnly date, ShiftType shiftType,
        ShiftRequestType requestType, CancellationToken ct = default)
    {
        return await _context.ShiftRequests
            .AnyAsync(r => r.UserId == userId &&
                           r.RequestDate == date &&
                           r.RequestType == requestType &&
                           r.Status != ShiftRequestStatus.Rejected &&
                           (r.ShiftType == shiftType || 
                            r.ShiftType == ShiftType.FullDay || 
                            shiftType == ShiftType.FullDay), ct);
    }

    public async Task<List<ShiftRequest>> ListByUserMonthAsync(
        Guid userId, DateOnly monthStart, DateOnly monthEnd, CancellationToken ct = default)
    {
        return await _context.ShiftRequests
            .Where(r => r.UserId == userId &&
                        r.RequestDate >= monthStart &&
                        r.RequestDate <= monthEnd &&
                        r.Status != ShiftRequestStatus.Rejected)
            .ToListAsync(ct);
    }

    public async Task AddAsync(ShiftRequest entity, CancellationToken ct = default)
    {
        await _context.ShiftRequests.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ShiftRequest entity, CancellationToken ct = default)
    {
        _context.ShiftRequests.Update(entity);
        await _context.SaveChangesAsync(ct);
    }
}
