using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public sealed class ScheduleSlotRepository : IScheduleSlotRepository
{
    private readonly AppDbContext _db;

    public ScheduleSlotRepository(AppDbContext db) => _db = db;

    public async Task<ScheduleSlot?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ScheduleSlots
            .AsNoTracking()
            .Include(s => s.Doctor)
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.SlotId == id, ct);
    }

    public async Task<IReadOnlyList<ScheduleSlot>> SearchAsync(
        Guid? doctorId,
        DateOnly? slotDate,
        SlotStatus? status,
        int skip,
        int take,
        CancellationToken ct = default)
    {
        var query = _db.ScheduleSlots.AsNoTracking().Include(s => s.Doctor).AsQueryable();
        if (doctorId.HasValue) query = query.Where(s => s.DoctorId == doctorId.Value);
        if (slotDate.HasValue) query = query.Where(s => s.SlotDate == slotDate.Value);
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        return await query
            .OrderBy(s => s.SlotDate)
            .ThenBy(s => s.StartTime)
            .Skip(skip).Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(
        Guid? doctorId,
        DateOnly? slotDate,
        SlotStatus? status,
        CancellationToken ct = default)
    {
        var query = _db.ScheduleSlots.AsNoTracking().AsQueryable();
        if (doctorId.HasValue) query = query.Where(s => s.DoctorId == doctorId.Value);
        if (slotDate.HasValue) query = query.Where(s => s.SlotDate == slotDate.Value);
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);
        return await query.CountAsync(ct);
    }

    public async Task<bool> HasOverlappingAsync(
        Guid doctorId,
        DateOnly slotDate,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? excludeSlotId,
        CancellationToken ct = default)
    {
        // chồng lấn: (existing.start < new.end) AND (existing.end > new.start)
        var query = _db.ScheduleSlots.AsNoTracking()
            .Where(s => s.DoctorId == doctorId
                && s.SlotDate == slotDate
                && s.StartTime < endTime
                && s.EndTime > startTime);
        if (excludeSlotId.HasValue) query = query.Where(s => s.SlotId != excludeSlotId.Value);
        return await query.AnyAsync(ct);
    }

    public async Task AddAsync(ScheduleSlot slot, CancellationToken ct = default)
    {
        await _db.ScheduleSlots.AddAsync(slot, ct);
    }
}