using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IScheduleSlotRepository (Module 8 — UC-15).
/// Read-only queries dùng AsNoTracking(§4.1).
/// </summary>
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
                .ThenInclude(a => a.PatientProfile)
                    .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(s => s.SlotId == id, ct);
    }

    public async Task<ScheduleSlot?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ScheduleSlots
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.SlotId == id, ct);
    }

    public async Task<IReadOnlyList<ScheduleSlot>> ListByDateAsync(
        DateOnly slotDate,
        Guid? doctorId = null,
        SlotStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        IQueryable<ScheduleSlot> query = _db.ScheduleSlots
            .AsNoTracking()
            .Include(s => s.Doctor)
            .Where(s => s.SlotDate == slotDate);

        if (doctorId.HasValue)
            query = query.Where(s => s.DoctorId == doctorId.Value);

        if (statusFilter.HasValue)
            query = query.Where(s => s.Status == statusFilter.Value);

        return await query
            .OrderBy(s => s.StartTime)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ScheduleSlot>> ListByRangeAsync(
        DateOnly from,
        DateOnly to,
        Guid? doctorId = null,
        SlotStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        IQueryable<ScheduleSlot> query = _db.ScheduleSlots
            .AsNoTracking()
            .Include(s => s.Doctor)
            .Include(s => s.Appointments)
                .ThenInclude(a => a.PatientProfile)
                    .ThenInclude(p => p.User)
            .Where(s => s.SlotDate >= from && s.SlotDate <= to);

        if (doctorId.HasValue)
            query = query.Where(s => s.DoctorId == doctorId.Value);

        if (statusFilter.HasValue)
            query = query.Where(s => s.Status == statusFilter.Value);

        return await query
            .OrderBy(s => s.SlotDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);
    }

    public async Task<bool> HasOverlapAsync(
        Guid doctorId,
        DateOnly slotDate,
        TimeOnly startTime,
        TimeOnly endTime,
        Guid? excludeSlotId = null,
        CancellationToken ct = default)
    {
        // Overlap: hai khoảng (a, b) và (c, d) giao nhau khi a < d && c < b.
        // Lưu ý: KHÔNG filter theo status — DB EXCLUDE constraint
        // `ex_schedule_slots_no_overlap` cũng không filter status, nên
        // nếu đã có slot CLOSED cùng giờ, insert slot OPEN mới sẽ violate
        // constraint tại DB. Phải check overlap cho MỌI status.
        IQueryable<ScheduleSlot> query = _db.ScheduleSlots
            .Where(s => s.DoctorId == doctorId
                     && s.SlotDate == slotDate
                     && s.StartTime < endTime
                     && startTime < s.EndTime);

        if (excludeSlotId.HasValue)
            query = query.Where(s => s.SlotId != excludeSlotId.Value);

        return await query.AnyAsync(ct);
    }

    public async Task<int> CountActiveAppointmentsAsync(Guid slotId, CancellationToken ct = default)
    {
        return await _db.Appointments
            .Where(a => a.SlotId == slotId && a.Status == AppointmentStatus.Booked)
            .CountAsync(ct);
    }

    public async Task<ScheduleSlot> AddAsync(ScheduleSlot slot, CancellationToken ct = default)
    {
        _db.ScheduleSlots.Add(slot);
        await _db.SaveChangesAsync(ct);
        return slot;
    }

    public async Task UpdateAsync(ScheduleSlot slot, CancellationToken ct = default)
    {
        _db.ScheduleSlots.Update(slot);
        await _db.SaveChangesAsync(ct);
    }
}