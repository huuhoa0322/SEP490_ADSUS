using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

/// <summary>
/// Implementation của IAppointmentService (Module 8 — UC-13, UC-14).
/// </summary>
public sealed class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly IScheduleSlotRepository _slotRepo;
    private readonly AppDbContext _db;

    public AppointmentService(
        IAppointmentRepository appointmentRepo,
        IScheduleSlotRepository slotRepo,
        AppDbContext db)
    {
        _appointmentRepo = appointmentRepo;
        _slotRepo = slotRepo;
        _db = db;
    }

    public async Task<IReadOnlyList<OpenSlotResponse>> ListOpenSlotsAsync(
        string? doctorId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        // BR-02: Chỉ trả về slot OPEN
        var statusFilter = SlotStatus.Open;

        IQueryable<ScheduleSlot> query = _db.ScheduleSlots
            .AsNoTracking()
            .Include(s => s.Doctor)
            .Include(s => s.Appointments)
            .Where(s => s.Status == statusFilter);

        if (doctorId != null && Guid.TryParse(doctorId, out var docGuid))
        {
            query = query.Where(s => s.DoctorId == docGuid);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(s => s.SlotDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(s => s.SlotDate <= toDate.Value);
        }

        // Filter slots that don't have BOOKED appointments
        var slots = await query
            .Where(s => !s.Appointments.Any(a => a.Status == AppointmentStatus.Booked))
            .OrderBy(s => s.SlotDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        return slots.Select(s => new OpenSlotResponse
        {
            SlotId = s.SlotId,
            DoctorId = s.DoctorId,
            DoctorName = s.Doctor.FullName,
            SlotDate = s.SlotDate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            CreatedAt = s.CreatedAt,
        }).ToList();
    }

    public async Task<IReadOnlyList<AppointmentSummaryResponse>> ListMyAppointmentsAsync(
        Guid patientProfileId,
        AppointmentStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        var appointments = await _appointmentRepo.ListByPatientAsync(patientProfileId, ct);

        if (statusFilter.HasValue)
        {
            appointments = appointments
                .Where(a => a.Status == statusFilter.Value)
                .ToList();
        }

        return appointments.Select(a => new AppointmentSummaryResponse
        {
            AppointmentId = a.AppointmentId,
            SlotDate = a.Slot.SlotDate,
            StartTime = a.Slot.StartTime,
            EndTime = a.Slot.EndTime,
            DoctorName = a.Slot.Doctor.FullName,
            Status = a.Status,
            CreatedAt = a.CreatedAt,
            Reason = a.Reason,
            CancellationReason = a.CancelledReason,
        }).ToList();
    }

    public async Task<AppointmentResponse?> GetByIdAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        var appointment = await _appointmentRepo.GetByIdAsync(appointmentId, ct);
        if (appointment == null) return null;

        return ToAppointmentResponse(appointment);
    }

    public async Task<AppointmentResponse> BookAppointmentAsync(
        Guid patientProfileId,
        BookAppointmentRequest request,
        CancellationToken ct = default)
    {
        // BR-01: Lấy slot với tracking để update
        var slot = await _slotRepo.GetByIdForUpdateAsync(request.ScheduleSlotId, ct)
            ?? throw new InvalidOperationException($"Slot '{request.ScheduleSlotId}' not found.");

        // BR-01: Slot phải có status = OPEN
        if (slot.Status != SlotStatus.Open)
        {
            throw new InvalidOperationException("Slot này không còn nhận đặt lịch.");
        }

        // BR-02: Kiểm tra không trùng booking
        var hasBooked = slot.Appointments.Any(a => a.Status == AppointmentStatus.Booked);
        if (hasBooked)
        {
            throw new InvalidOperationException("Slot này đã có người đặt.");
        }

        // Tạo appointment
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = request.ScheduleSlotId,
            PatientProfileId = patientProfileId,
            Reason = request.Reason,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Update slot status
        slot.Status = SlotStatus.Booked;
        slot.UpdatedAt = DateTime.UtcNow;

        await _appointmentRepo.CreateAsync(appointment, ct);
        await _slotRepo.UpdateAsync(slot, ct);

        // Load navigation properties for response
        appointment.Slot = slot;

        return ToAppointmentResponse(appointment);
    }

    public async Task<AppointmentResponse> CancelAppointmentAsync(
        Guid appointmentId,
        Guid patientProfileId,
        CancelAppointmentRequest request,
        CancellationToken ct = default)
    {
        // Validate cancellation reason
        if (string.IsNullOrWhiteSpace(request.CancellationReason))
        {
            throw new InvalidOperationException("Lý do hủy lịch là bắt buộc.");
        }

        // Get appointment với tracking
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct)
            ?? throw new InvalidOperationException($"Appointment '{appointmentId}' not found.");

        // BR-01: Chỉ patient sở hữu mới được hủy
        if (appointment.PatientProfileId != patientProfileId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền hủy lịch hẹn này.");
        }

        // BR-01: Chỉ BOOKED mới được hủy
        if (appointment.Status != AppointmentStatus.Booked)
        {
            throw new InvalidOperationException("Chỉ lịch hẹn đang đặt mới được hủy.");
        }

        // Update appointment
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledReason = request.CancellationReason;
        appointment.UpdatedAt = DateTime.UtcNow;

        // Update slot status về OPEN
        var slot = appointment.Slot;
        slot.Status = SlotStatus.Open;
        slot.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return ToAppointmentResponse(appointment);
    }

    private static AppointmentResponse ToAppointmentResponse(Appointment a)
    {
        return new AppointmentResponse
        {
            AppointmentId = a.AppointmentId,
            ScheduleSlotId = a.SlotId,
            SlotDate = a.Slot.SlotDate,
            StartTime = a.Slot.StartTime,
            EndTime = a.Slot.EndTime,
            DoctorName = a.Slot.Doctor?.FullName ?? string.Empty,
            Status = a.Status,
            Reason = a.Reason,
            CancellationReason = a.CancelledReason,
            CalendarSyncedAt = a.CalendarSyncedAt,
            CreatedAt = a.CreatedAt,
        };
    }
}
