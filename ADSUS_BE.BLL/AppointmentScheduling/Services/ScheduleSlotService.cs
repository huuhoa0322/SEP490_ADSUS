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
/// BR-02: Closed có thể mở lại (ReopenSlotAsync).
/// Doctor tự quản lý lịch của chính mình; hệ thống tự sinh ca mặc định T2-CN 8h-12h &amp; 13h-17h.
/// </summary>
public sealed class ScheduleSlotService : IScheduleSlotService
{
    /// <summary>16 ca 30 phút mỗi ngày T2-CN: 8h-12h (8 ca) + 13h-17h (8 ca).</summary>
    private static readonly (TimeOnly Start, TimeOnly End)[] DefaultRanges =
    {
        // Buổi sáng
        (new TimeOnly(8, 0),  new TimeOnly(8, 30)),
        (new TimeOnly(8, 30), new TimeOnly(9, 0)),
        (new TimeOnly(9, 0),  new TimeOnly(9, 30)),
        (new TimeOnly(9, 30), new TimeOnly(10, 0)),
        (new TimeOnly(10, 0), new TimeOnly(10, 30)),
        (new TimeOnly(10, 30),new TimeOnly(11, 0)),
        (new TimeOnly(11, 0), new TimeOnly(11, 30)),
        (new TimeOnly(11, 30),new TimeOnly(12, 0)),
        // Buổi chiều
        (new TimeOnly(13, 0), new TimeOnly(13, 30)),
        (new TimeOnly(13, 30),new TimeOnly(14, 0)),
        (new TimeOnly(14, 0), new TimeOnly(14, 30)),
        (new TimeOnly(14, 30),new TimeOnly(15, 0)),
        (new TimeOnly(15, 0), new TimeOnly(15, 30)),
        (new TimeOnly(15, 30),new TimeOnly(16, 0)),
        (new TimeOnly(16, 0), new TimeOnly(16, 30)),
        (new TimeOnly(16, 30),new TimeOnly(17, 0)),
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

    public async Task<(IReadOnlyList<ScheduleSlotResponse> Items, int TotalCount)> ListSlotsAsync(
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        Guid? doctorId = null,
        SlotStatus? statusFilter = null,
        int page = 1,
        int pageSize = 200, // 14 ngày × 16 slots = 224 max, lấy 200 đủ cho hầu hết case
        CancellationToken ct = default)
    {
        if (!doctorId.HasValue || doctorId.Value == Guid.Empty)
            throw new InvalidOperationException("doctorId is required.");

        var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var to = toDate ?? from.AddDays(21); // 3 tuần = 21 ngày

        if (to < from)
            throw new InvalidOperationException("toDate must not be before fromDate.");

        // Auto-sinh dựa trên data đã có (1 query duy nhất)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var targetEndDate = today.AddDays(20); // 21 ngày (today..today+20)
        var allSlots = await _repo.ListByRangeAsync(today, targetEndDate, doctorId, null, ct);
        await EnsureMissingSlotsAsync(doctorId.Value, today, targetEndDate, allSlots, ct);

        // Lấy data trong range user yêu cầu (sau khi auto-sinh)
        var slots = await _repo.ListByRangeAsync(from, to, doctorId, statusFilter, ct);

        // Apply pagination
        var totalCount = slots.Count;
        var skipCount = (page - 1) * pageSize;
        var pagedSlots = slots
            .Skip(skipCount)
            .Take(pageSize)
            .Select(MapToResponse)
            .ToList();

        return (pagedSlots, totalCount);
    }

    /// <summary>
    /// Tự sinh các ca mặc định còn thiếu trong range (in-memory check dựa trên existingSlots).
    /// Tối ưu: 1 query ListByRange (thay vì 42 queries HasOverlap).
    /// Idempotent: nếu slot đã tồn tại sẽ bị catch 23505/23P01.
    /// </summary>
    private async Task EnsureMissingSlotsAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        IReadOnlyList<ScheduleSlot> existingSlots,
        CancellationToken ct = default)
    {
        if (doctorId == Guid.Empty)
            throw new InvalidOperationException("doctorId is required.");

        // Build lookup dict: DateOnly → list slots của ngày đó
        var slotsByDay = new Dictionary<DateOnly, List<ScheduleSlot>>();
        foreach (var s in existingSlots)
        {
            if (!slotsByDay.TryGetValue(s.SlotDate, out var list))
            {
                list = new List<ScheduleSlot>();
                slotsByDay[s.SlotDate] = list;
            }
            list.Add(s);
        }

        var now = DateTime.UtcNow;
        var newSlots = new List<ScheduleSlot>();

        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            var daySlots = slotsByDay.GetValueOrDefault(day) ?? new List<ScheduleSlot>();

            foreach (var (start, end) in DefaultRanges)
            {
                var startDateTime = day.ToDateTime(start, DateTimeKind.Utc);
                if (startDateTime <= now) continue;

                // Check overlap in-memory (không cần query DB)
                var hasOverlap = daySlots.Any(s => s.StartTime < end && start < s.EndTime);
                if (hasOverlap) continue;

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

        // Insert từng slot, idempotent catch
        foreach (var s in newSlots)
        {
            try
            {
                await _repo.AddAsync(s, ct);
                // Track slot vừa insert để check overlap cho các slot tiếp theo (cùng request)
                if (!slotsByDay.TryGetValue(s.SlotDate, out var list))
                {
                    list = new List<ScheduleSlot>();
                    slotsByDay[s.SlotDate] = list;
                }
                list.Add(s);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx
                && (pgEx.SqlState == "23505" || pgEx.SqlState == "23P01"))
            {
                // 23505: Unique constraint; 23P01: EXCLUDE constraint → slot đã tồn tại (idempotent)
            }
        }
    }

    /// <summary>
    /// Backward-compatible wrapper. Gọi 1 query ListByRange rồi pass cho EnsureMissingSlotsAsync.
    /// </summary>
    public async Task EnsureUpcomingSlotsAsync(
        Guid doctorId,
        CancellationToken ct = default)
    {
        if (doctorId == Guid.Empty)
            throw new InvalidOperationException("doctorId is required.");

        // Doctor phải tồn tại và là Doctor.
        var doctor = await _userRepo.GetByIdAsync(doctorId, ct);
        if (doctor is null || doctor.Role != UserRole.Doctor)
            throw new InvalidOperationException($"User '{doctorId}' is not a valid Doctor.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var targetEndDate = today.AddDays(20);
        var existingSlots = await _repo.ListByRangeAsync(today, targetEndDate, doctorId, null, ct);

        await EnsureMissingSlotsAsync(doctorId, today, targetEndDate, existingSlots, ct);
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

    public async Task<(int SuccessCount, int ErrorCount)> CreateOvertimeSlotsAsync(
        CreateOvertimeSlotsRequest request,
        Guid doctorId,
        CancellationToken ct = default)
    {
        if (doctorId == Guid.Empty)
            throw new InvalidOperationException("doctorId is required.");

        var doctor = await _userRepo.GetByIdAsync(doctorId, ct);
        if (doctor is null || doctor.Role != UserRole.Doctor)
        {
            throw new InvalidOperationException($"User '{doctorId}' is not a valid Doctor.");
        }

        int successCount = 0;
        int errorCount = 0;
        var now = DateTime.UtcNow;
        
        var existingSlotsEnum = await _repo.ListByRangeAsync(request.VisitDate, request.VisitDate, doctorId, null, ct);
        var existingSlots = new System.Collections.Generic.List<ScheduleSlot>(existingSlotsEnum);
        
        // 17h đến 20h = 6 ca x 30 phút
        for (int i = 0; i < 6; i++)
        {
            var start = new TimeOnly(17, 0).AddMinutes(i * 30);
            var end = start.AddMinutes(30);
            
            var startDateTime = request.VisitDate.ToDateTime(start, DateTimeKind.Utc);
            if (startDateTime <= now)
            {
                errorCount++;
                continue;
            }

            var hasOverlap = existingSlots.Any(s => s.StartTime < end && start < s.EndTime);
                
            if (hasOverlap)
            {
                errorCount++;
                continue;
            }

            var slot = new ScheduleSlot
            {
                SlotId = Guid.NewGuid(),
                DoctorId = doctorId,
                SlotDate = request.VisitDate,
                StartTime = start,
                EndTime = end,
                Status = SlotStatus.Open,
                CreatedAt = now,
                UpdatedAt = now,
            };

            await _repo.AddAsync(slot, ct);
            existingSlots.Add(slot);
            successCount++;
        }
        
        return (successCount, errorCount);
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

    public async Task<ScheduleSlotResponse> ReopenSlotAsync(Guid slotId, CancellationToken ct = default)
    {
        var slot = await _repo.GetByIdForUpdateAsync(slotId, ct);
        if (slot is null)
            throw new InvalidOperationException($"Slot '{slotId}' not found.");

        if (slot.Status == SlotStatus.Open)
            throw new InvalidOperationException("Slot is already open.");

        slot.Status = SlotStatus.Open;
        slot.UpdatedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(slot, ct);
        return MapToResponse(slot);
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

        // 14 ngày T2-CN (Thứ 2 đến Chủ nhật, 2 tuần).
        for (var d = 0; d < 14; d++)
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
                && (pgEx.SqlState == "23505" || pgEx.SqlState == "23P01"))
            {
                // 23505: Unique constraint (uq_schedule_slots_start) bị vi phạm
                // 23P01: EXCLUDE constraint (ex_schedule_slots_no_overlap) bị vi phạm
                // → slot đã tồn tại. Bỏ qua để giữ idempotent.
            }
        }
    }

    private static ScheduleSlotResponse MapToResponse(ScheduleSlot slot)
    {
        // Lấy các booking đang ACTIVE (BOOKED, không phải CANCELLED)
        var bookedAppointments = slot.Appointments?
            .Where(a => a.Status == AppointmentStatus.Booked)
            .Select(a => new BookedAppointmentInfo
            {
                AppointmentId = a.AppointmentId,
                PatientProfileId = a.PatientProfileId,
                PatientFullName = a.PatientProfile?.User?.FullName ?? "Unknown",
                Reason = a.Reason,
                Status = a.Status,
            })
            .ToList() ?? new List<BookedAppointmentInfo>();

        return new ScheduleSlotResponse
        {
            SlotId = slot.SlotId,
            DoctorId = slot.DoctorId,
            DoctorName = slot.Doctor?.FullName ?? string.Empty,
            SlotDate = slot.SlotDate,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            Status = slot.Status,
            ActiveAppointmentsCount = bookedAppointments.Count,
            BookedAppointments = bookedAppointments,
            CreatedAt = slot.CreatedAt,
            UpdatedAt = slot.UpdatedAt,
        };
    }
}
