using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

/// <summary>
/// Implementation của IAppointmentService (Module 8 — UC-13, UC-14).
/// </summary>
public sealed class AppointmentService : IAppointmentService
{
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly IScheduleSlotRepository _slotRepo;
    private readonly IPatientProfileRepository _profileRepo;
    private readonly INotificationService _notificationService;
    private readonly ICaseService _caseService;
    private readonly NoShowService _noShowService;
    private readonly AppDbContext _db;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(
        IAppointmentRepository appointmentRepo,
        IScheduleSlotRepository slotRepo,
        IPatientProfileRepository profileRepo,
        INotificationService notificationService,
        ICaseService caseService,
        NoShowService noShowService,
        AppDbContext db,
        ILogger<AppointmentService> logger)
    {
        _appointmentRepo = appointmentRepo;
        _slotRepo = slotRepo;
        _profileRepo = profileRepo;
        _notificationService = notificationService;
        _caseService = caseService;
        _noShowService = noShowService;
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OpenSlotResponse>> ListOpenSlotsAsync(
        string? doctorId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        // Giới hạn: trong vòng 2 tuần (mặc định nếu không truyền from/to).
        var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var to = toDate ?? from.AddDays(14);

        Guid? docGuid = doctorId != null && Guid.TryParse(doctorId, out var parsed) ? parsed : null;

        // BR-02: Chỉ trả về slot OPEN. Đi qua repository (như mọi service khác trong module
        // này) thay vì query thẳng AppDbContext — query thẳng cần kết nối DB thật, không thể
        // test bằng mock repository.
        var rangeSlots = await _slotRepo.ListByRangeAsync(from, to, docGuid, SlotStatus.Open, ct);

        // Chỉ trả về slot của bác sĩ ACTIVE, và loại slot đã có appointment BOOKED.
        var slots = rangeSlots
            .Where(s => s.Doctor.Status == UserStatus.Active)
            .Where(s => !s.Appointments.Any(a => a.Status == AppointmentStatus.Booked))
            .OrderBy(s => s.SlotDate)
            .ThenBy(s => s.StartTime);

        return slots.Select(s => new OpenSlotResponse
        {
            SlotId = s.SlotId,
            DoctorId = s.DoctorId,
            DoctorName = s.Doctor.FullName,
            DoctorStatus = s.Doctor.Status,
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

        // =====================================================
        // VALIDATION RULES - Chống spam đặt lịch
        // =====================================================

        // Rule 4: Minimum 2h advance booking
        var slotDateTime = slot.SlotDate.ToDateTime(slot.StartTime);
        var now = DateTime.UtcNow;
        var hoursUntilSlot = (slotDateTime - now).TotalHours;
        if (hoursUntilSlot < 2)
        {
            throw new InvalidOperationException(
                "Phải đặt lịch trước tối thiểu 2 giờ. Vui lòng chọn ca khám khác.");
        }

        // Rule 1: Max 3 active appointments
        var activeAppointments = await _db.Appointments
            .Where(a => a.PatientProfileId == patientProfileId
                && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Approved))
            .CountAsync(ct);
        if (activeAppointments >= 3)
        {
            throw new InvalidOperationException(
                "Bạn đã có 3 lịch hẹn đang chờ. Vui lòng hoàn thành hoặc hủy lịch cũ trước khi đặt mới.");
        }

        // Rule 3: Không đặt trùng ngày (QUAN TRỌNG NHẤT)
        var hasSameDayAppointment = await _db.Appointments
            .Include(a => a.Slot)
            .AnyAsync(a =>
                a.PatientProfileId == patientProfileId
                && a.Slot.SlotDate == slot.SlotDate
                && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Approved),
                ct);
        if (hasSameDayAppointment)
        {
            throw new InvalidOperationException(
                $"Bạn đã có lịch khám vào ngày {slot.SlotDate:dd/MM/yyyy}. Vui lòng hủy lịch cũ trước khi đặt lịch mới.");
        }

        // Rule 2: Giới hạn đặt trong phạm vi 3 ngày
        var next3Days = DateOnly.FromDateTime(now.AddDays(3));
        var hasAppointmentWithin3Days = await _db.Appointments
            .Include(a => a.Slot)
            .AnyAsync(a =>
                a.PatientProfileId == patientProfileId
                && a.Slot.SlotDate > slot.SlotDate
                && a.Slot.SlotDate <= next3Days
                && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Approved),
                ct);
        if (hasAppointmentWithin3Days)
        {
            throw new InvalidOperationException(
                "Bạn đã có lịch hẹn trong vòng 3 ngày tới. Vui lòng đặt lịch sau khi đã hoàn thành lịch hiện tại.");
        }

        // Rule 5: Max 2 appointments/day cho cùng ngày (bao gồm slot đang đặt)
        var todayAppointments = await _db.Appointments
            .Where(a => a.PatientProfileId == patientProfileId
                && a.Slot.SlotDate == slot.SlotDate
                && a.Status == AppointmentStatus.Booked)
            .CountAsync(ct);
        if (todayAppointments >= 2)
        {
            throw new InvalidOperationException(
                $"Ngày {slot.SlotDate:dd/MM/yyyy} đã có 2 lịch hẹn. Vui lòng chọn ngày khác.");
        }

        // =====================================================
        // END VALIDATION RULES
        // =====================================================

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

        // Tạo Case nếu có symptoms (từ Mobile booking)
        if (request.Symptoms?.Count > 0)
        {
            var caseId = await _caseService.CreateFromBookingAsync(
                patientProfileId,
                slot.DoctorId,
                slot.SlotDate,
                request.Symptoms,
                ct);

            appointment.CaseId = caseId;

            _logger.LogInformation(
                "Case {CaseId} created from appointment booking for appointment {AppointmentId}",
                caseId, appointment.AppointmentId);
        }

        // Update slot status
        slot.Status = SlotStatus.Booked;
        slot.UpdatedAt = DateTime.UtcNow;

        await _appointmentRepo.CreateAsync(appointment, ct);
        await _slotRepo.UpdateAsync(slot, ct);

        // Load navigation properties for response
        appointment.Slot = slot;

        // Send notification to patient (best effort - don't fail the booking if notification fails)
        try
        {
            var patientProfile = await _profileRepo.GetByIdAsync(patientProfileId, ct);
            if (patientProfile != null)
            {
                _logger.LogInformation(
                    "[NOTIF-DEBUG] Preparing to send booking notification to user {UserId} for appointment {AppointmentId}",
                    patientProfile.UserId, appointment.AppointmentId);

                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientProfile.UserId,
                    Type = "appointment_booking",
                    Title = "Xác nhận đặt lịch khám",
                    Body = $"Bạn đã đặt lịch khám với BS. {slot.Doctor.FullName} vào ngày {slot.SlotDate:dd/MM/yyyy} lúc {slot.StartTime}.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["appointmentId"] = appointment.AppointmentId.ToString(),
                        ["slotId"] = slot.SlotId.ToString()
                    }
                }, ct);

                _logger.LogInformation(
                    "[NOTIF-SUCCESS] Sent booking notification to user {UserId} for appointment {AppointmentId}",
                    patientProfile.UserId, appointment.AppointmentId);
            }
            else
            {
                _logger.LogWarning(
                    "[NOTIF-WARN] No patient profile found for patientProfileId {PatientProfileId}",
                    patientProfileId);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the booking
            _logger.LogWarning(ex,
                "[NOTIF-ERROR] Failed to send booking notification for appointment {AppointmentId}: {Message}",
                appointment.AppointmentId, ex.Message);
        }

        // Gửi notification cho doctor phụ trách
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = slot.DoctorId,
                Type = "new_appointment_booking",
                Title = "Có lịch hẹn mới",
                Body = $"Bệnh nhân đã đặt lịch khám ngày {slot.SlotDate:dd/MM/yyyy} lúc {slot.StartTime}.",
                DeepLink = $"/appointments",
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NOTIF-ERROR] Failed to send booking notification to doctor for appointment {AppointmentId}", appointment.AppointmentId);
        }

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

        // Cập nhật Case status nếu có liên kết
        if (appointment.CaseId.HasValue)
        {
            var medicalCase = await _db.Cases
                .FirstOrDefaultAsync(c => c.CaseId == appointment.CaseId, ct);

            if (medicalCase != null)
            {
                medicalCase.Status = CaseStatus.Cancelled;
                medicalCase.UpdatedAt = DateTime.UtcNow;
            }
        }

        // Update slot status về OPEN
        var slot = appointment.Slot;
        slot.Status = SlotStatus.Open;
        slot.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Send notification to patient about cancellation (best effort - don't fail cancellation if notification fails)
        try
        {
            var patientProfile = await _profileRepo.GetByIdAsync(appointment.PatientProfileId, ct);
            if (patientProfile != null)
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientProfile.UserId,
                    Type = "appointment_cancellation",
                    Title = "Lịch khám đã bị hủy",
                    Body = $"Lịch khám với BS. {slot.Doctor.FullName} vào ngày {slot.SlotDate:dd/MM/yyyy} đã bị hủy.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["appointmentId"] = appointment.AppointmentId.ToString()
                    }
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send cancellation notification for appointment {AppointmentId}", appointment.AppointmentId);
        }

        // Gửi notification cho doctor khi bệnh nhân hủy lịch
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = slot.DoctorId,
                Type = "appointment_cancelled_by_patient",
                Title = "Bệnh nhân hủy lịch khám",
                Body = $"Bệnh nhân đã hủy lịch khám ngày {slot.SlotDate:dd/MM/yyyy} lúc {slot.StartTime}.",
                DeepLink = $"/appointments",
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send cancellation notification to doctor for appointment {AppointmentId}", appointment.AppointmentId);
        }

        return ToAppointmentResponse(appointment);
    }

    public async Task<AppointmentResponse> CheckinAppointmentAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        // Lấy appointment với tracking để update
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct)
            ?? throw new InvalidOperationException($"Appointment '{appointmentId}' not found.");

        // Chỉ appointment đang BOOKED mới được checkin
        if (appointment.Status != AppointmentStatus.Booked)
        {
            throw new InvalidOperationException("Chỉ lịch hẹn đang ở trạng thái ĐÃ ĐẶT mới được checkin.");
        }

        // Xử lý No-Show nếu đã quá grace time
        var noShowResult = await _noShowService.ProcessNoShowAsync(appointment, ct);
        if (noShowResult.WasProcessed)
        {
            throw new InvalidOperationException(
                $"Lịch hẹn đã tự động hủy do không check-in trong 15 phút kể từ lịch hẹn.");
        }

        // Cập nhật Appointment: Booked → Approved
        appointment.Status = AppointmentStatus.Approved;
        appointment.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Appointment {AppointmentId} checked in by nurse. Status: {Status}",
            appointmentId, appointment.Status);

        // Gửi notification cho patient khi checkin thành công
        try
        {
            var patientProfile = await _profileRepo.GetByIdAsync(appointment.PatientProfileId, ct);
            if (patientProfile != null)
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientProfile.UserId,
                    Type = "appointment_checkin",
                    Title = "Đã check-in thành công",
                    Body = $"Bạn đã được check-in cho lịch khám ngày {appointment.Slot.SlotDate:dd/MM/yyyy} lúc {appointment.Slot.StartTime} với BS. {appointment.Slot.Doctor.FullName}.",
                    DeepLink = $"/appointments/{appointment.AppointmentId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["appointmentId"] = appointment.AppointmentId.ToString()
                    }
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send checkin notification to patient for appointment {AppointmentId}", appointmentId);
        }

        // Gửi notification cho doctor khi patient checkin
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = appointment.Slot.DoctorId,
                Type = "patient_checked_in",
                Title = "Bệnh nhân đã check-in",
                Body = $"Bệnh nhân đã check-in cho lịch khám ngày {appointment.Slot.SlotDate:dd/MM/yyyy} lúc {appointment.Slot.StartTime}.",
                DeepLink = $"/appointments/{appointment.AppointmentId}",
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send checkin notification to doctor for appointment {AppointmentId}", appointmentId);
        }

        return ToAppointmentResponse(appointment);
    }

    public async Task<AppointmentResponse> CheckinByCaseIdAsync(
        Guid caseId,
        CancellationToken ct = default)
    {
        // Tìm appointment đang BOOKED liên quan đến case này
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .FirstOrDefaultAsync(a => a.CaseId == caseId, ct)
            ?? throw new InvalidOperationException($"Không tìm thấy lịch hẹn cho case '{caseId}'.");

        // Kiểm tra status hợp lệ
        if (appointment.Status == AppointmentStatus.Approved)
        {
            throw new InvalidOperationException("Bệnh nhân đã được check-in trước đó.");
        }

        if (appointment.Status == AppointmentStatus.Completed)
        {
            throw new InvalidOperationException("Lịch hẹn đã hoàn thành, không thể check-in.");
        }

        if (appointment.Status == AppointmentStatus.Cancelled || appointment.Status == AppointmentStatus.NoShow)
        {
            throw new InvalidOperationException($"Lịch hẹn đã bị hủy (status: {appointment.Status}), không thể check-in.");
        }

        // Xử lý No-Show nếu đã quá grace time
        var noShowResult = await _noShowService.ProcessNoShowAsync(appointment, ct);
        if (noShowResult.WasProcessed)
        {
            throw new InvalidOperationException(
                $"Lịch hẹn đã tự động hủy do không check-in trong 15 phút kể từ lịch hẹn.");
        }

        // Chuyển sang Approved
        appointment.Status = AppointmentStatus.Approved;
        appointment.UpdatedAt = DateTime.UtcNow;

        // Cập nhật Case status nếu có liên kết
        if (appointment.CaseId.HasValue)
        {
            var medicalCase = await _db.Cases
                .FirstOrDefaultAsync(c => c.CaseId == appointment.CaseId, ct);

            if (medicalCase != null && medicalCase.Status == CaseStatus.Booked)
            {
                medicalCase.Status = CaseStatus.InProgress;
                medicalCase.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Appointment {AppointmentId} checked in by nurse via CaseId {CaseId}. Status: {Status}",
            appointment.AppointmentId, caseId, appointment.Status);

        // Gửi notification cho patient khi checkin thành công
        try
        {
            var patientProfile = await _profileRepo.GetByIdAsync(appointment.PatientProfileId, ct);
            if (patientProfile != null)
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientProfile.UserId,
                    Type = "appointment_checkin",
                    Title = "Đã check-in thành công",
                    Body = $"Bạn đã được check-in cho lịch khám ngày {appointment.Slot.SlotDate:dd/MM/yyyy} lúc {appointment.Slot.StartTime} với BS. {appointment.Slot.Doctor.FullName}.",
                    DeepLink = $"/appointments/{appointment.AppointmentId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["appointmentId"] = appointment.AppointmentId.ToString()
                    }
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send checkin notification to patient for appointment {AppointmentId}", appointment.AppointmentId);
        }

        // Gửi notification cho doctor khi patient checkin
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = appointment.Slot.DoctorId,
                Type = "patient_checked_in",
                Title = "Bệnh nhân đã check-in",
                Body = $"Bệnh nhân đã check-in cho lịch khám ngày {appointment.Slot.SlotDate:dd/MM/yyyy} lúc {appointment.Slot.StartTime}.",
                DeepLink = $"/appointments/{appointment.AppointmentId}",
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send checkin notification to doctor for appointment {AppointmentId}", appointment.AppointmentId);
        }

        return ToAppointmentResponse(appointment);
    }

    public async Task<IReadOnlyList<DoctorPatientAppointmentResponse>> ListForDoctorAsync(
        Guid doctorId,
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        var appointments = await _appointmentRepo.ListByDoctorAsync(doctorId, fromDate, toDate, ct);

        // Hiện cả BOOKED và APPROVED — Cancelled và Completed ẩn hẳn.
        // Lý do: Approved = bệnh nhân đã đến (nurse checkin) — vẫn cần hiện trên màn "Lịch bệnh nhân"
        // để bác sĩ biết ai đã đến, không bị mất khỏi danh sách khám ngay từ khi được checkin.
        return appointments
            .Where(a => a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Approved)
            .Select(a => new DoctorPatientAppointmentResponse
            {
                AppointmentId = a.AppointmentId,
                SlotDate = a.Slot.SlotDate,
                StartTime = a.Slot.StartTime,
                EndTime = a.Slot.EndTime,
                PatientProfileId = a.PatientProfileId,
                PatientFullName = a.PatientProfile.User.FullName,
                Reason = a.Reason,
            })
            .ToList();
    }

    public async Task<CheckinQueueResponse> GetCheckinQueueAsync(
        DateOnly date,
        string? search = null,
        CancellationToken ct = default)
    {
        // Lấy tất cả appointments trong ngày đang ở Booked hoặc Approved
        var appointments = await _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Where(a => a.Slot.SlotDate == date)
            .Where(a => a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Approved)
            .Where(a => a.Slot.Status != SlotStatus.Closed)
            .OrderBy(a => a.Slot.StartTime)
            .ToListAsync(ct);

        // Filter by search if provided
        if (!string.IsNullOrWhiteSpace(search))
        {
            appointments = appointments
                .Where(a => a.PatientProfile.User.FullName.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (a.PatientProfile.User.Phone != null && a.PatientProfile.User.Phone.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var items = appointments.Select(a => new CheckinQueueItemResponse
        {
            AppointmentId = a.AppointmentId,
            SlotTime = a.Slot.SlotDate.ToDateTime(a.Slot.StartTime),
            PatientFullName = a.PatientProfile.User.FullName,
            PatientPhone = a.PatientProfile.User.Phone,
            PatientProfileId = a.PatientProfileId,
            CaseId = a.CaseId ?? Guid.Empty,
            Reason = a.Reason,
            DoctorName = a.Slot.Doctor.FullName,
            Status = a.Status,
        }).ToList();

        return new CheckinQueueResponse
        {
            Items = items,
            TotalCount = items.Count,
        };
    }

    private static AppointmentResponse ToAppointmentResponse(Appointment a, Guid? caseId = null)
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
            CaseId = caseId ?? a.CaseId,
        };
    }
}
