using Microsoft.EntityFrameworkCore;

using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using FluentValidation;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

/// <summary>
/// Service cho ScheduleSlot (Module 8 — UC-15).
/// BR-01: VisitDate + StartTime > now (UTC); range > 15 phút; không overlap.
/// BR-02: Closed là terminal.
/// Doctor tự quản lý lịch của chính mình; hệ thống tự sinh ca mặc định T2-T6 8h-12h &amp; 13h-17h.
/// </summary>
public sealed class ScheduleSlotService : IScheduleSlotService
{
    /// <summary>2 ca mặc định mỗi ngày T2-T6.</summary>
    private static readonly (TimeOnly Start, TimeOnly End)[] DefaultRanges =
    {
        (new TimeOnly(8, 0), new TimeOnly(12, 0)),
        (new TimeOnly(13, 0), new TimeOnly(17, 0)),
    };

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
        Guid doctorId,
        CreateScheduleSlotRequest request,
        CancellationToken ct = default)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        // Doctor phải tồn tại và là Doctor.
        var doctor = await _userRepo.GetByIdAsync(doctorId, ct);
        if (doctor is null || doctor.Role != UserRole.Doctor)
        {
            throw new InvalidOperationException(
                $"User '{doctorId}' is not a valid Doctor.");
        }

        var hasOverlap = await _repo.HasOverlapAsync(
            doctorId, request.VisitDate, request.StartTime, request.EndTime,
            excludeSlotId: null, ct);
        if (hasOverlap)
        {
            throw new InvalidOperationException(
                $"Slot overlaps with an existing slot on {request.VisitDate:yyyy-MM-dd}.");
        }

        var now = DateTime.UtcNow;
        var slot = new ScheduleSlot
        {
            SlotId = Guid.NewGuid(),
            DoctorId = doctorId,
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

    public async Task<ScheduleSlotResponse> UpdateSlotAsync(
        Guid slotId,
        UpdateScheduleSlotRequest request,
        CancellationToken ct = default)
    {
        var slot = await _repo.GetByIdForUpdateAsync(slotId, ct);
        if (slot is null)
            throw new InvalidOperationException($"Slot '{slotId}' not found.");

        // BR-02: Closed là terminal.
        if (slot.Status == SlotStatus.Closed)
            throw new InvalidOperationException("Cannot update a closed slot.");

        // Validate BR-01 với StartTime/EndTime mới.
        var probe = new CreateScheduleSlotRequest
        {
            VisitDate = slot.SlotDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
        };
        var validation = await _validator.ValidateAsync(probe, ct);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Check overlap với slot khác (loại trừ chính slot đang update).
        var hasOverlap = await _repo.HasOverlapAsync(
            slot.DoctorId, slot.SlotDate,
            request.StartTime, request.EndTime,
            excludeSlotId: slotId, ct);
        if (hasOverlap)
            throw new InvalidOperationException(
                $"Updated slot overlaps with another slot on {slot.SlotDate:yyyy-MM-dd}.");

        slot.StartTime = request.StartTime;
        slot.EndTime = request.EndTime;
        slot.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(slot, ct);
        return MapToResponse(slot);
    }

    public async Task<CloseSlotImpactResponse> CloseSlotAsync(
        Guid slotId,
        bool forceClose,
        CancellationToken ct = default)
    {
        var slot = await _repo.GetByIdForUpdateAsync(slotId, ct);
        if (slot is null)
            throw new InvalidOperationException($"Slot '{slotId}' not found.");

        if (slot.Status == SlotStatus.Closed)
            throw new InvalidOperationException("Slot is already closed.");

        var activeCount = slot.Appointments.Count(a => a.Status == AppointmentStatus.Booked);

        if (activeCount > 0 && !forceClose)
        {
            return new CloseSlotImpactResponse
            {
                SlotId = slotId,
                AffectedBookingsCount = activeCount,
            };
        }

        slot.Status = SlotStatus.Closed;
        slot.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(slot, ct);

        return new CloseSlotImpactResponse
        {
            SlotId = slotId,
            AffectedBookingsCount = activeCount,
        };
    }

    public async Task EnsureDefaultSlotsAsync(
        Guid doctorId,
        DateOnly weekStart,
        CancellationToken ct = default)
    {
        // weekStart phải là T2 (Monday).
        if (weekStart.DayOfWeek != DayOfWeek.Monday)
        {
            throw new InvalidOperationException("weekStart must be a Monday.");
        }

        var doctor = await _userRepo.GetByIdAsync(doctorId, ct);
        if (doctor is null || doctor.Role != UserRole.Doctor)
            throw new InvalidOperationException($"User '{doctorId}' is not a valid Doctor.");

        var now = DateTime.UtcNow;
        var newSlots = new List<ScheduleSlot>();

        // 5 ngày T2-T6.
        for (var d = 0; d < 5; d++)
        {
            var day = weekStart.AddDays(d);

            // Với mỗi range mặc định, kiểm tra đã có slot OPEN overlap chưa.
            foreach (var (start, end) in DefaultRanges)
            {
                // Skip ca trong quá khứ.
                var startDateTime = day.ToDateTime(start, DateTimeKind.Utc);
                if (startDateTime <= now) continue;

                var hasOverlap = await _repo.HasOverlapAsync(
                    doctorId, day, start, end,
                    excludeSlotId: null, ct);
                if (hasOverlap) continue; // Doctor đã có slot trong range này (tách ca hoặc tự thêm).

                newSlots.Add(new ScheduleSlot
                {
                    SlotId = Guid.NewGuid(),
                    DoctorId = doctorId,
                    SlotDate = day,
                    StartTime = start,
                    EndTime = end,
                    Status = SlotStatus.Open,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }

        foreach (var s in newSlots)
        {
            try
            {
                await _repo.AddAsync(s, ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx
                && pgEx.SqlState == "23505")
            {
                // Unique constraint "uq_schedule_slots_start" bị vi phạm = slot đã có
                // (do request song song hoặc user bấm 2 lần). Bỏ qua để giữ idempotent.
            }
        }
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
