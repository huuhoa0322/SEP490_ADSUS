using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

/// <summary>
/// Service cho ScheduleSlot (Module 8 — UC-15).
/// BR-01: slot không trong quá khứ; range > 15 phút; không overlap.
/// BR-02: Closed là terminal; không reopen.
/// </summary>
public sealed class ScheduleSlotService : IScheduleSlotService
{
    private readonly IScheduleSlotRepository _repo;
    private readonly IUserRepository _userRepo;
    private readonly IValidator<CreateScheduleSlotRequest> _validator;

    public ScheduleSlotService(
        IScheduleSlotRepository repo,
        IUserRepository userRepo,
        IValidator<CreateScheduleSlotRequest> validator)
    {
        _repo = repo;
        _userRepo = userRepo;
        _validator = validator;
    }

    public async Task<IReadOnlyList<ScheduleSlotResponse>> ListSlotsAsync(
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? doctorId = null,
        SlotStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        // Default range: hôm nay → +30 ngày nếu không truyền.
        var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var to = toDate ?? from.AddDays(30);

        if (to < from)
            throw new InvalidOperationException("toDate must not be before fromDate.");

        var slots = await _repo.ListByRangeAsync(from, to, doctorId, statusFilter, ct);
        return slots.Select(MapToResponse).ToList();
    }

    public async Task<ScheduleSlotResponse?> GetSlotAsync(Guid slotId, CancellationToken ct = default)
    {
        var slot = await _repo.GetByIdAsync(slotId, ct);
        return slot is null ? null : MapToResponse(slot);
    }

    public async Task<ScheduleSlotResponse> CreateSlotAsync(
        CreateScheduleSlotRequest request,
        CancellationToken ct = default)
    {
        // 1. Validate request fields (BR-01: quá khứ, range, order).
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        // 2. Verify Doctor tồn tại và có role = DOCTOR hoặc NURSE.
        // (UC-15 BR: Doctor/Nurse tạo slot — Nurse vẫn được assign slot cho Doctor khác.)
        var doctor = await _userRepo.GetByIdAsync(request.DoctorId, ct);
        if (doctor is null || (doctor.Role != UserRole.Doctor && doctor.Role != UserRole.Nurse))
        {
            throw new InvalidOperationException(
                $"User '{request.DoctorId}' is not a valid Doctor/Nurse.");
        }

        // 3. Check overlap (BR-01: cùng Doctor, cùng ngày, range giao nhau).
        var hasOverlap = await _repo.HasOverlapAsync(
            request.DoctorId,
            request.VisitDate,
            request.StartTime,
            request.EndTime,
            excludeSlotId: null,
            ct);
        if (hasOverlap)
        {
            throw new InvalidOperationException(
                $"Slot overlaps with an existing slot for the same doctor on {request.VisitDate:yyyy-MM-dd}.");
        }

        // 4. Tạo entity (status = OPEN mặc định, created_at/updated_at tự sinh).
        var now = DateTime.UtcNow;
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = request.DoctorId,
            SlotDate = request.VisitDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = SlotStatus.Open,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await _repo.AddAsync(slot, ct);
        return MapToResponse(slot);
    }

    public async Task<CloseSlotImpactResponse> CloseSlotAsync(
        Guid slotId,
        bool forceClose,
        CancellationToken ct = default)
    {
        // 1. Load slot (tracking) — cần Include(Appointments) để đếm booking.
        var slot = await _repo.GetByIdForUpdateAsync(slotId, ct);
        if (slot is null)
        {
            throw new InvalidOperationException($"Slot '{slotId}' not found.");
        }

        // 2. BR-02: Closed là terminal.
        if (slot.Status == SlotStatus.Closed)
        {
            throw new InvalidOperationException("Slot is already closed.");
        }

        // 3. Đếm booking đang active (UC-15 AF-02).
        var activeCount = slot.Appointments.Count(a => a.Status == AppointmentStatus.Booked);

        if (activeCount > 0 && !forceClose)
        {
            // Trả về impact thay vì throw — FE sẽ hiển thị dialog cảnh báo.
            return new CloseSlotImpactResponse
            {
                SlotId = slotId,
                AffectedBookingsCount = activeCount,
            };
        }

        // 4. Force close hoặc không có booking → set Closed.
        slot.Status = SlotStatus.Closed;
        slot.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(slot, ct);

        return new CloseSlotImpactResponse
        {
            SlotId = slotId,
            AffectedBookingsCount = activeCount,
        };
    }

    private static ScheduleSlotResponse MapToResponse(ScheduleSlot slot)
    {
        return new ScheduleSlotResponse
        {
            SlotId = slot.SlotId,
            DoctorId = slot.DoctorId,
            DoctorName = slot.Doctor?.FullName ?? string.Empty,
            SlotDate = slot.SlotDate,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            Status = slot.Status,
            ActiveAppointmentsCount = slot.Appointments?
                .Count(a => a.Status == AppointmentStatus.Booked) ?? 0,
            CreatedAt = slot.CreatedAt,
            UpdatedAt = slot.UpdatedAt,
        };
    }
}