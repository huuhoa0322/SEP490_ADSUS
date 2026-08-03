using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Exceptions;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

/// <summary>
/// Module 8 UC-15 (#46, #47, #48, #49) — Doctor/Nurse schedule slot management.
///
/// Quyết định F1 (UCS): KHÔNG viết status FULL. Slot.status chỉ OPEN -> CLOSED.
/// List appointments không đổi slot status; close slot không đổi appointment status.
/// </summary>
public sealed class ScheduleSlotService : IScheduleSlotService
{
    private readonly IScheduleSlotRepository _slots;
    private readonly IAppointmentRepository _appointments;
    private readonly IUserRepository _users;
    private readonly AppDbContext _db;

    public ScheduleSlotService(
        IScheduleSlotRepository slots,
        IAppointmentRepository appointments,
        IUserRepository users,
        AppDbContext db)
    {
        _slots = slots;
        _appointments = appointments;
        _users = users;
        _db = db;
    }

    public async Task<PagedResult<ScheduleSlotResponse>> SearchAsync(
        Guid? doctorId, DateOnly? slotDate, string? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        SlotStatus? statusEnum = status?.ToUpperInvariant() switch
        {
            "OPEN" => SlotStatus.Open,
            "CLOSED" => SlotStatus.Closed,
            _ => null,
        };

        var skip = (page - 1) * pageSize;
        var entities = await _slots.SearchAsync(doctorId, slotDate, statusEnum, skip, pageSize, ct);
        var total = await _slots.CountAsync(doctorId, slotDate, statusEnum, ct);

        var items = entities.Select(ToResponse).ToList();
        return new PagedResult<ScheduleSlotResponse>(items, total, page, pageSize);
    }

    public async Task<ScheduleSlotResponse> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var slot = await _slots.GetByIdAsync(id, ct)
            ?? throw new ScheduleSlotNotFoundException(id);
        return ToResponse(slot);
    }

    public async Task<ScheduleSlotResponse> CreateAsync(
        CreateScheduleSlotRequest req, CancellationToken ct = default)
    {
        // Verify Doctor exists with role=DOC
        var doctor = await _users.GetByIdAsync(req.DoctorId, ct)
            ?? throw new DoctorNotFoundException(req.DoctorId);
        if (doctor.Role != UserRole.Doctor && doctor.Role != UserRole.Nurse)
            throw new DoctorNotFoundException(req.DoctorId);

        var start = TimeOnly.Parse(req.StartTime);
        var end = TimeOnly.Parse(req.EndTime);

        // App-level overlap pre-check (best-effort; DB constraint vẫn enforce)
        if (await _slots.HasOverlappingAsync(req.DoctorId, req.SlotDate, start, end, null, ct))
            throw new SlotOverlapException();

        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = req.DoctorId,
            SlotDate = req.SlotDate,
            StartTime = start,
            EndTime = end,
            Status = SlotStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await _slots.AddAsync(slot, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            // race vs giST EXCLUDE constraint or uq_schedule_slots_start
            await tx.RollbackAsync(ct);
            throw new SlotOverlapException();
        }

        return await GetByIdAsync(slot.SlotId, ct);
    }

    public async Task<ScheduleSlotResponse> UpdateStatusAsync(
        Guid id, UpdateScheduleSlotStatusRequest req, CancellationToken ct = default)
    {
        var slot = await _db.ScheduleSlots.FirstOrDefaultAsync(s => s.SlotId == id, ct)
            ?? throw new ScheduleSlotNotFoundException(id);
        if (slot.Status == SlotStatus.Closed)
            throw new SlotAlreadyClosedException();

        slot.Status = SlotStatus.Closed;
        slot.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<PagedResult<AppointmentSummaryResponse>> ListAppointmentsBySlotAsync(
        Guid slotId, int page, int pageSize, CancellationToken ct = default)
    {
        // Ensure slot exists for proper 404
        var slot = await _slots.GetByIdAsync(slotId, ct)
            ?? throw new ScheduleSlotNotFoundException(slotId);

        var skip = (page - 1) * pageSize;
        var (entities, total) = await _appointments.ListBySlotPagedAsync(slotId, skip, pageSize, ct);

        var items = entities.Select(a => new AppointmentSummaryResponse(
            a.AppointmentId,
            a.PatientProfileId,
            a.PatientProfile?.User?.FullName ?? string.Empty,
            a.Status.ToString().ToUpperInvariant(),
            a.Reason,
            a.CancelledReason,
            a.CreatedAt,
            a.UpdatedAt)).ToList();

        return new PagedResult<AppointmentSummaryResponse>(items, total, page, pageSize);
    }

    private static ScheduleSlotResponse ToResponse(ScheduleSlot s) =>
        new(s.SlotId, s.DoctorId, s.Doctor?.FullName ?? string.Empty,
            s.SlotDate, s.StartTime, s.EndTime,
            s.Status.ToString().ToUpperInvariant(),
            s.CreatedAt, s.UpdatedAt);
}