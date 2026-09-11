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

    private static readonly TimeZoneInfo VietnamZone = GetVietnamTimeZone();

    private static TimeZoneInfo GetVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.CreateCustomTimeZone("Vietnam Standard Time", TimeSpan.FromHours(7), "Vietnam Standard Time", "Vietnam Standard Time");
            }
        }
    }

    private static DateTime GetNowVietnam()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, VietnamZone);
    }

    public async Task<IReadOnlyList<OpenSlotResponse>> ListOpenSlotsAsync(
        string? doctorId = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        // Giới hạn: trong vòng 30 ngày (mặc định nếu không truyền from/to).
        var from = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var to = toDate ?? from.AddDays(30);

        Guid? docGuid = doctorId != null && Guid.TryParse(doctorId, out var parsed) ? parsed : null;

        // BR-02: Chỉ trả về slot OPEN. Đi qua repository (như mọi service khác trong module
        // này) thay vì query thẳng AppDbContext — query thẳng cần kết nối DB thật, không thể
        // test bằng mock repository.
        var rangeSlots = await _slotRepo.ListByRangeAsync(from, to, docGuid, SlotStatus.Open, ct);

        var nowVn = GetNowVietnam();
        var todayVn = DateOnly.FromDateTime(nowVn);
        var currentTimeVn = TimeOnly.FromDateTime(nowVn);

        // Chỉ trả về slot của bác sĩ ACTIVE, loại slot đã có appointment BOOKED,
        // và ẩn toàn bộ các slot trong quá khứ (SlotDate < today hoặc SlotDate == today && StartTime <= currentTime).
        var slots = rangeSlots
            .Where(s => s.Doctor.Status == UserStatus.Active)
            .Where(s => !s.Appointments.Any(a => a.Status == AppointmentStatus.Booked))
            .Where(s => s.SlotDate > todayVn || (s.SlotDate == todayVn && s.StartTime > currentTimeVn))
            .OrderBy(s => s.SlotDate)
            .ThenBy(s => s.StartTime);

        return slots.Select(s => new OpenSlotResponse
        {
            SlotId = s.SlotId,
            DoctorId = s.DoctorId,
            DoctorName = s.Doctor.FullName,
            DoctorStatus = s.Doctor.Status,
            DoctorGender = s.Doctor.Gender, // 2026-01: thêm gender của bác sĩ
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
            ScheduleSlotId = a.SlotId,
            DoctorId = a.Slot.DoctorId,
            SlotDate = a.Slot.SlotDate,
            StartTime = a.Slot.StartTime,
            EndTime = a.Slot.EndTime,
            DoctorName = a.Slot.Doctor.FullName,
            Status = a.Status,
            CreatedAt = a.CreatedAt,
            Reason = a.Reason,
            CancellationReason = a.CancelledReason,
            CaseId = a.CaseId,
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

        // Không cho phép đặt lịch vào khung giờ đã qua
        var nowVn = GetNowVietnam();
        var todayVn = DateOnly.FromDateTime(nowVn);
        var currentTimeVn = TimeOnly.FromDateTime(nowVn);
        if (slot.SlotDate < todayVn || (slot.SlotDate == todayVn && slot.StartTime <= currentTimeVn))
        {
            throw new InvalidOperationException("Không thể đặt lịch vào khung giờ đã qua.");
        }

        // =====================================================
        // VALIDATION RULES - Chống spam đặt lịch
        // =====================================================

        var now = DateTime.UtcNow;

        // Rule 1: Max 3 active appointments (BOOKED)
        var activeAppointments = await _db.Appointments
            .Where(a => a.PatientProfileId == patientProfileId
                && a.Status == AppointmentStatus.Booked)
            .CountAsync(ct);
        if (activeAppointments >= 3)
        {
            throw new InvalidOperationException(
                "Bạn đã có 3 lịch hẹn đang chờ. Vui lòng hoàn thành hoặc hủy lịch cũ trước khi đặt mới.");
        }

        // Rule 2: 1 bệnh nhân - 1 ngày - tối đa 1 lịch (KHÔNG phân biệt bác sĩ)
        var hasSameDayAppointment = await _db.Appointments
            .Include(a => a.Slot).ThenInclude(s => s.Doctor)
            .AnyAsync(a =>
                a.PatientProfileId == patientProfileId
                && a.Slot.SlotDate == slot.SlotDate
                && a.Status == AppointmentStatus.Booked,
                ct);
        if (hasSameDayAppointment)
        {
            var existingAppointment = await _db.Appointments
                .Include(a => a.Slot).ThenInclude(s => s.Doctor)
                .FirstOrDefaultAsync(a =>
                    a.PatientProfileId == patientProfileId
                    && a.Slot.SlotDate == slot.SlotDate
                    && a.Status == AppointmentStatus.Booked,
                    ct);
            var doctorName = existingAppointment?.Slot?.Doctor?.FullName ?? "bác sĩ";
            throw new InvalidOperationException(
                $"Bạn đã có lịch khám với {doctorName} vào ngày {slot.SlotDate:dd/MM/yyyy}. " +
                "Mỗi ngày chỉ được đặt tối đa 1 lịch. Vui lòng hủy lịch cũ trước khi đặt lịch mới.");
        }

        // Rule 2: Giới hạn đặt trong phạm vi 3 ngày
        var next3Days = DateOnly.FromDateTime(now.AddDays(3));
        var hasAppointmentWithin3Days = await _db.Appointments
            .Include(a => a.Slot)
            .AnyAsync(a =>
                a.PatientProfileId == patientProfileId
                && a.Slot.SlotDate > slot.SlotDate
                && a.Slot.SlotDate <= next3Days
                && a.Status == AppointmentStatus.Booked,
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

        var patientProfile = await _profileRepo.GetByIdAsync(patientProfileId, ct);

        // Send notification to patient (best effort - don't fail the booking if notification fails)
        try
        {
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
            var userId = patientProfile?.UserId ?? Guid.Empty;
            var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            var patientName = user?.FullName ?? "Bệnh nhân";
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = slot.DoctorId,
                Type = "new_appointment_booking",
                Title = "Có lịch hẹn mới",
                Body = $"Bệnh nhân {patientName} đã đặt lịch khám ngày {slot.SlotDate:dd/MM/yyyy} lúc {slot.StartTime}.",
                DeepLink = $"/patients/{appointment.PatientProfileId}",
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

    public async Task<AppointmentResponse> CreateFollowUpAppointmentAsync(
        Guid doctorId,
        FollowUpAppointmentRequest request,
        CancellationToken ct = default)
    {
        // 1. Kiểm tra slot tồn tại và lấy thông tin slot
        var slot = await _db.ScheduleSlots
            .Include(s => s.Doctor)
            .FirstOrDefaultAsync(s => s.SlotId == request.ScheduleSlotId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy khung giờ này.");

        // 2. Phải là slot của chính bác sĩ đang hẹn
        if (slot.DoctorId != doctorId)
        {
            throw new InvalidOperationException("Chỉ được chọn khung giờ của chính bạn.");
        }

        // 3. Slot phải đang OPEN
        if (slot.Status != SlotStatus.Open)
        {
            throw new InvalidOperationException("Khung giờ này đã được đặt hoặc đã đóng.");
        }

        // 4. Bệnh nhân không được hẹn trùng giờ
        var hasConflict = await _db.Appointments.AnyAsync(a =>
            a.PatientProfileId == request.PatientProfileId &&
            a.SlotId == request.ScheduleSlotId &&
            a.Status != AppointmentStatus.Cancelled &&
            a.Status != AppointmentStatus.NoShow, ct);

        if (hasConflict)
        {
            throw new InvalidOperationException("Bệnh nhân đã có lịch hẹn trong khung giờ này.");
        }

        // Không cho phép đặt lịch tái khám vào khung giờ đã qua
        var nowVn = GetNowVietnam();
        var todayVn = DateOnly.FromDateTime(nowVn);
        var currentTimeVn = TimeOnly.FromDateTime(nowVn);
        if (slot.SlotDate < todayVn || (slot.SlotDate == todayVn && slot.StartTime <= currentTimeVn))
        {
            throw new InvalidOperationException("Không thể đặt lịch tái khám vào khung giờ đã qua.");
        }

        // 5. Patient Profile phải tồn tại
        var patientProfile = await _profileRepo.GetByIdAsync(request.PatientProfileId, ct);
        if (patientProfile == null)
        {
            throw new KeyNotFoundException("Không tìm thấy hồ sơ bệnh nhân.");
        }

        // 6. Validation: Max 3 active appointments cho bệnh nhân
        var activeAppointments = await _db.Appointments
            .Where(a => a.PatientProfileId == request.PatientProfileId
                && a.Status == AppointmentStatus.Booked)
            .CountAsync(ct);
        if (activeAppointments >= 3)
        {
            throw new InvalidOperationException(
                "Bệnh nhân đã có 3 lịch hẹn đang chờ. Vui lòng hoàn thành hoặc hủy lịch cũ trước khi đặt mới.");
        }

        // 7. Validation: 1 bệnh nhân - 1 ngày - tối đa 1 lịch
        var hasSameDayAppointment = await _db.Appointments
            .Include(a => a.Slot).ThenInclude(s => s.Doctor)
            .AnyAsync(a =>
                a.PatientProfileId == request.PatientProfileId
                && a.Slot.SlotDate == slot.SlotDate
                && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Completed),
                ct);
        if (hasSameDayAppointment)
        {
            var existingAppointment = await _db.Appointments
                .Include(a => a.Slot).ThenInclude(s => s.Doctor)
                .FirstOrDefaultAsync(a =>
                    a.PatientProfileId == request.PatientProfileId
                    && a.Slot.SlotDate == slot.SlotDate
                    && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Completed),
                    ct);
            var existingDoctorName = existingAppointment?.Slot?.Doctor?.FullName ?? "bác sĩ";
            throw new InvalidOperationException(
                $"Bệnh nhân đã có lịch khám với {existingDoctorName} vào ngày {slot.SlotDate:dd/MM/yyyy}. " +
                "Mỗi ngày chỉ được đặt tối đa 1 lịch. Vui lòng hủy lịch cũ trước khi đặt lịch mới.");
        }

        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = request.ScheduleSlotId,
            PatientProfileId = request.PatientProfileId,
            Reason = request.Reason,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        slot.Status = SlotStatus.Booked;
        slot.UpdatedAt = DateTime.UtcNow;

        await _appointmentRepo.CreateAsync(appointment, ct);
        await _slotRepo.UpdateAsync(slot, ct);
        
        // Notify patient
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = patientProfile.UserId,
                Type = "appointment_booking",
                Title = "Lịch hẹn tái khám",
                Body = $"Bác sĩ {slot.Doctor.FullName} đã hẹn tái khám cho bạn vào ngày {slot.SlotDate:dd/MM/yyyy} lúc {slot.StartTime}.",
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NOTIF-ERROR] Failed to send follow-up notification to patient");
        }

        // Notify doctor
        try
        {
            var patientName = patientProfile.User?.FullName ?? "bệnh nhân";
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = slot.DoctorId,
                Type = "appointment_booking",
                Title = "Tạo lịch tái khám thành công",
                Body = $"Bạn đã tạo lịch hẹn tái khám thành công cho bệnh nhân {patientName} vào ngày {slot.SlotDate:dd/MM/yyyy} lúc {slot.StartTime}.",
                DeepLink = $"/patients/{appointment.PatientProfileId}",
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NOTIF-ERROR] Failed to send follow-up notification to doctor");
        }

        // Provide necessary nav props for response
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
            var patientProfile = await _profileRepo.GetByIdAsync(appointment.PatientProfileId, ct);
            var patientName = patientProfile?.User?.FullName ?? "bệnh nhân";
            
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = slot.DoctorId,
                Type = "appointment_cancelled_by_patient",
                Title = "Bệnh nhân hủy lịch khám",
                Body = $"Bệnh nhân {patientName} đã hủy lịch khám ngày {slot.SlotDate:dd/MM/yyyy} lúc {slot.StartTime}.",
                DeepLink = $"/patients/{appointment.PatientProfileId}",
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

        // Cập nhật Appointment: Booked → Completed (patient check-in thành công)
        appointment.Status = AppointmentStatus.Completed;
        appointment.UpdatedAt = DateTime.UtcNow;

        // Cập nhật Case status nếu có liên kết
        if (appointment.CaseId.HasValue)
        {
            var medicalCase = await _db.Cases
                .FirstOrDefaultAsync(c => c.CaseId == appointment.CaseId.Value, ct);

            if (medicalCase != null && medicalCase.Status == CaseStatus.Booked)
            {
                medicalCase.Status = CaseStatus.InProgress;
                medicalCase.UpdatedAt = DateTime.UtcNow;
            }
        }

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
        // Tìm appointment đang BOOKED liên quan đến case này (ưu tiên lịch mới nhất nếu case từng đổi lịch)
        var appointment = await _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(a => a.CaseId == caseId && a.Status == AppointmentStatus.Booked, ct);

        if (appointment == null)
        {
            var existingAppt = await _db.Appointments
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(a => a.CaseId == caseId, ct)
                ?? throw new InvalidOperationException($"Không tìm thấy lịch hẹn cho case '{caseId}'.");

            if (existingAppt.Status == AppointmentStatus.Completed)
            {
                throw new InvalidOperationException("Bệnh nhân đã được check-in trước đó.");
            }

            if (existingAppt.Status == AppointmentStatus.Cancelled || existingAppt.Status == AppointmentStatus.NoShow)
            {
                throw new InvalidOperationException($"Lịch hẹn đã bị hủy (status: {existingAppt.Status}), không thể check-in.");
            }

            throw new InvalidOperationException($"Không tìm thấy lịch hẹn ở trạng thái ĐÃ ĐẶT cho case '{caseId}'.");
        }

        // Xử lý No-Show nếu đã quá grace time
        var noShowResult = await _noShowService.ProcessNoShowAsync(appointment, ct);
        if (noShowResult.WasProcessed)
        {
            throw new InvalidOperationException(
                $"Lịch hẹn đã tự động hủy do không check-in trong 15 phút kể từ lịch hẹn.");
        }

        // Chuyển sang Completed (patient check-in thành công)
        appointment.Status = AppointmentStatus.Completed;
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

        // Chỉ hiện BOOKED — Cancelled, Completed và mọi trạng thái khác đều ẩn.
        // Lý do: màn "Lịch bệnh nhân" của bác sĩ chỉ quan tâm lịch hẹn còn hiệu lực chưa diễn ra;
        // trạng thái "đã đến/đang khám" được theo dõi qua Case.Status (InProgress), không qua
        // Appointment.Status — hai khái niệm tách biệt theo quyết định của user (2026-09-10).
        return appointments
            .Where(a => a.Status == AppointmentStatus.Booked)
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

    public Task<CheckinQueueResponse> GetCheckinQueueAsync(
        DateOnly date,
        string? search = null,
        CancellationToken ct = default)
    {
        return GetCheckinQueueAsync(date, date, search, null, 1, 1000, ct);
    }

    public async Task<CheckinQueueResponse> GetCheckinQueueAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        string? search = null,
        string? status = null,
        int page = 1,
        int pageSize = 15,
        CancellationToken ct = default)
    {
        var effectiveFrom = fromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveTo = toDate ?? effectiveFrom;
        if (effectiveFrom > effectiveTo)
        {
            (effectiveFrom, effectiveTo) = (effectiveTo, effectiveFrom);
        }

        var baseQuery = _db.Appointments
            .AsNoTracking()
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Where(a => a.Slot.SlotDate >= effectiveFrom && a.Slot.SlotDate <= effectiveTo);

        // Tìm kiếm ở mức EF Core query: FullName, Phone, DoctorName, Reason
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            baseQuery = baseQuery.Where(a =>
                (a.PatientProfile != null && a.PatientProfile.User != null && a.PatientProfile.User.FullName.ToLower().Contains(term)) ||
                (a.PatientProfile != null && a.PatientProfile.User != null && a.PatientProfile.User.Phone != null && a.PatientProfile.User.Phone.Contains(term)) ||
                (a.Slot != null && a.Slot.Doctor != null && a.Slot.Doctor.FullName.ToLower().Contains(term)) ||
                (a.Reason != null && a.Reason.ToLower().Contains(term)));
        }

        // Đếm số lượng theo trạng thái trên toàn bộ mốc thời gian đã chọn (không phụ thuộc phân trang)
        var bookedCount = await baseQuery.CountAsync(a => a.Status == AppointmentStatus.Booked, ct);
        var checkedInCount = await baseQuery.CountAsync(a => a.Status == AppointmentStatus.Completed, ct);
        var cancelledCount = await baseQuery.CountAsync(a => a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.NoShow, ct);

        // Filter theo status: ALL, BOOKED, APPROVED (Đã check-in), CANCELLED
        var query = baseQuery;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = status.Trim().Replace("_", "");
            if (string.Equals(normalized, "APPROVED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "CHECKEDIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                // Bệnh nhân check-in trong DB mang trạng thái COMPLETED
                query = query.Where(a => a.Status == AppointmentStatus.Completed);
            }
            else if (string.Equals(normalized, "CANCELLED", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(normalized, "NOSHOW", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(a => a.Status == AppointmentStatus.Cancelled || a.Status == AppointmentStatus.NoShow);
            }
            else if (Enum.TryParse<AppointmentStatus>(normalized, true, out var parsedStatus))
            {
                query = query.Where(a => a.Status == parsedStatus);
            }
        }
        else if (string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Completed) && a.Slot.Status != SlotStatus.Closed);
        }

        var totalCount = await query.CountAsync(ct);

        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize is < 1 or > 1000 ? 15 : pageSize;

        // Sắp xếp: Đang chờ check-in (Booked = 0) lên đầu -> Đã check-in (Completed = 1) ở giữa -> Đã hủy / vắng mặt (Cancelled/NoShow = 2) ở cuối.
        // Trong cùng mỗi nhóm: sắp xếp tăng dần theo thời gian (SlotDate, StartTime).
        var appointments = await query
            .OrderBy(a => a.Status == AppointmentStatus.Booked ? 0
                        : (a.Status == AppointmentStatus.Completed ? 1 : 2))
            .ThenBy(a => a.Slot.SlotDate)
            .ThenBy(a => a.Slot.StartTime)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .ToListAsync(ct);

        var items = appointments.Select(a => new CheckinQueueItemResponse
        {
            AppointmentId = a.AppointmentId,
            SlotTime = a.Slot.SlotDate.ToDateTime(a.Slot.StartTime),
            PatientFullName = a.PatientProfile?.User?.FullName ?? string.Empty,
            PatientPhone = a.PatientProfile?.User?.Phone,
            PatientProfileId = a.PatientProfileId,
            CaseId = a.CaseId ?? Guid.Empty,
            Reason = a.Reason,
            DoctorName = a.Slot?.Doctor?.FullName ?? string.Empty,
            Status = a.Status,
        }).ToList();

        return new CheckinQueueResponse
        {
            Items = items,
            Page = effectivePage,
            PageSize = effectivePageSize,
            TotalCount = totalCount,
            BookedCount = bookedCount,
            CheckedInCount = checkedInCount,
            CancelledCount = cancelledCount,
        };
    }

    public async Task<AppointmentResponse> RescheduleAppointmentAsync(
        Guid oldAppointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.RescheduleReason))
        {
            throw new InvalidOperationException("Lý do đổi lịch là bắt buộc.");
        }

        var oldAppointment = await _db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(a => a.Case)
            .FirstOrDefaultAsync(a => a.AppointmentId == oldAppointmentId, ct)
            ?? throw new KeyNotFoundException($"Appointment '{oldAppointmentId}' not found.");

        // Validate appointment status and determine scenario
        bool isScenario1 = oldAppointment.Status == AppointmentStatus.Booked;
        bool isScenario2 = oldAppointment.Status == AppointmentStatus.Completed;
        bool isScenario3 = oldAppointment.Status == AppointmentStatus.Cancelled || oldAppointment.Status == AppointmentStatus.NoShow;

        if (!isScenario1 && !isScenario2 && !isScenario3)
        {
            throw new InvalidOperationException($"Không thể đổi lịch hẹn ở trạng thái '{oldAppointment.Status}'.");
        }

        if (isScenario2 && oldAppointment.Case != null &&
            (oldAppointment.Case.Status == CaseStatus.Confirmed || oldAppointment.Case.Status == CaseStatus.End))
        {
            throw new InvalidOperationException("Ca khám đã kết thúc, không thể đổi lịch.");
        }

        // Validate new slot
        var newSlot = await _db.ScheduleSlots
            .Include(s => s.Doctor)
            .Include(s => s.Appointments)
            .FirstOrDefaultAsync(s => s.SlotId == request.NewScheduleSlotId, ct)
            ?? throw new KeyNotFoundException($"Khung giờ '{request.NewScheduleSlotId}' không tồn tại.");

        if (newSlot.Status != SlotStatus.Open)
        {
            throw new InvalidOperationException("Khung giờ này không còn nhận đặt lịch.");
        }

        if (newSlot.Appointments.Any(a => a.Status == AppointmentStatus.Booked))
        {
            throw new InvalidOperationException("Khung giờ này đã có người đặt.");
        }

        // Chặn đổi lịch sang khung giờ đã qua
        var nowVn = GetNowVietnam();
        var todayVn = DateOnly.FromDateTime(nowVn);
        var currentTimeVn = TimeOnly.FromDateTime(nowVn);
        if (newSlot.SlotDate < todayVn || (newSlot.SlotDate == todayVn && newSlot.StartTime <= currentTimeVn))
        {
            throw new InvalidOperationException("Không thể đổi lịch sang khung giờ đã qua.");
        }

        // Validate same-day conflicting appointment for patient (excluding old appointment)
        var hasSameDayConflict = await _db.Appointments
            .Include(a => a.Slot)
            .AnyAsync(a => a.PatientProfileId == oldAppointment.PatientProfileId
                        && a.AppointmentId != oldAppointment.AppointmentId
                        && a.Slot != null
                        && a.Slot.SlotDate == newSlot.SlotDate
                        && (a.Status == AppointmentStatus.Booked || a.Status == AppointmentStatus.Completed), ct);

        if (hasSameDayConflict)
        {
            throw new InvalidOperationException($"Bệnh nhân đã có lịch khám vào ngày {newSlot.SlotDate:dd/MM/yyyy}.");
        }

        var now = DateTime.UtcNow;
        var newAppointmentStatus = request.AutoCheckin ? AppointmentStatus.Completed : AppointmentStatus.Booked;
        var newCaseStatus = request.AutoCheckin ? CaseStatus.InProgress : CaseStatus.Booked;

        var newAppointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = newSlot.SlotId,
            PatientProfileId = oldAppointment.PatientProfileId,
            Status = newAppointmentStatus,
            Reason = !string.IsNullOrWhiteSpace(request.NewReason) ? request.NewReason : oldAppointment.Reason,
            CaseId = oldAppointment.CaseId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        var isRelational = _db.Database.IsRelational();
        var transaction = isRelational ? await _db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            if (isScenario1)
            {
                // Scenario 1: BOOKED
                oldAppointment.Status = AppointmentStatus.Cancelled;
                oldAppointment.CancelledReason = $"Đổi lịch: {request.RescheduleReason}";
                oldAppointment.UpdatedAt = now;

                if (oldAppointment.Slot != null)
                {
                    oldAppointment.Slot.Status = SlotStatus.Open;
                    oldAppointment.Slot.UpdatedAt = now;
                }

                if (oldAppointment.Case != null)
                {
                    if (newSlot.DoctorId != oldAppointment.Slot?.DoctorId)
                    {
                        oldAppointment.Case.DoctorId = newSlot.DoctorId;
                    }
                    oldAppointment.Case.Status = newCaseStatus;
                    oldAppointment.Case.UpdatedAt = now;
                }
            }
            else if (isScenario2)
            {
                // Scenario 2: COMPLETED or APPROVED
                // Old appointment and old slot remain untouched
                if (oldAppointment.Case != null)
                {
                    if (newSlot.DoctorId != oldAppointment.Slot?.DoctorId)
                    {
                        oldAppointment.Case.DoctorId = newSlot.DoctorId;
                    }
                    // Case remains InProgress (do not downgrade to Booked)
                    oldAppointment.Case.Status = CaseStatus.InProgress;
                    oldAppointment.Case.UpdatedAt = now;
                }
            }
            else if (isScenario3)
            {
                // Scenario 3: CANCELLED or NO_SHOW
                // Old appointment and old slot remain untouched
                if (oldAppointment.Case != null)
                {
                    if (newSlot.DoctorId != oldAppointment.Slot?.DoctorId)
                    {
                        oldAppointment.Case.DoctorId = newSlot.DoctorId;
                    }

                    if (oldAppointment.Case.Status == CaseStatus.Cancelled)
                    {
                        oldAppointment.Case.Status = newCaseStatus;
                        oldAppointment.Case.UpdatedAt = now;
                    }
                }
            }

            // New slot is booked
            newSlot.Status = SlotStatus.Booked;
            newSlot.UpdatedAt = now;

            // Add new appointment
            await _db.Appointments.AddAsync(newAppointment, ct);

            await _db.SaveChangesAsync(ct);

            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }

        // Set navigation properties for mapper
        newAppointment.Slot = newSlot;
        newAppointment.Case = oldAppointment.Case;
        newAppointment.PatientProfile = oldAppointment.PatientProfile;

        // Post-commit notifications (inside try-catch, best effort)
        // 1. Patient notification
        try
        {
            var patientUserId = oldAppointment.PatientProfile?.UserId;
            if (patientUserId.HasValue && patientUserId.Value != Guid.Empty)
            {
                var newDoctorName = newSlot.Doctor?.FullName ?? "Bác sĩ";
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientUserId.Value,
                    Type = "appointment_rescheduled",
                    Title = "Lịch hẹn đã được đổi",
                    Body = $"Lịch hẹn đã được đổi sang {newSlot.SlotDate:dd/MM/yyyy} lúc {newSlot.StartTime} với BS. {newDoctorName}.",
                    DeepLink = $"/appointments/{newAppointment.AppointmentId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["oldAppointmentId"] = oldAppointment.AppointmentId.ToString(),
                        ["newAppointmentId"] = newAppointment.AppointmentId.ToString(),
                        ["slotId"] = newSlot.SlotId.ToString()
                    }
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NOTIF-ERROR] Failed to send reschedule notification to patient for appointment {AppointmentId}", newAppointment.AppointmentId);
        }

        // 2. New Doctor notification
        try
        {
            var patientName = oldAppointment.PatientProfile?.User?.FullName ?? "Bệnh nhân";
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = newSlot.DoctorId,
                Type = "doctor_appointment_rescheduled",
                Title = "Lịch hẹn được chuyển đến",
                Body = $"BN {patientName} được chuyển sang ca của bạn lúc {newSlot.StartTime} ngày {newSlot.SlotDate:dd/MM/yyyy}.",
                DeepLink = $"/patients/{newAppointment.PatientProfileId}",
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = newAppointment.AppointmentId.ToString(),
                    ["slotId"] = newSlot.SlotId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NOTIF-ERROR] Failed to send reschedule notification to new doctor for appointment {AppointmentId}", newAppointment.AppointmentId);
        }

        // 3. Old Doctor notification (if doctor changed)
        var oldDoctorId = oldAppointment.Slot?.DoctorId;
        if (oldDoctorId.HasValue && oldDoctorId.Value != Guid.Empty && oldDoctorId.Value != newSlot.DoctorId)
        {
            try
            {
                var patientName = oldAppointment.PatientProfile?.User?.FullName ?? "Bệnh nhân";
                var newDoctorName = newSlot.Doctor?.FullName ?? "bác sĩ khác";
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = oldDoctorId.Value,
                    Type = "doctor_appointment_transferred",
                    Title = "Lịch hẹn đã chuyển bác sĩ",
                    Body = $"BN {patientName} đã được chuyển sang BS. {newDoctorName}.",
                    DeepLink = $"/patients/{newAppointment.PatientProfileId}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["oldAppointmentId"] = oldAppointment.AppointmentId.ToString(),
                        ["newAppointmentId"] = newAppointment.AppointmentId.ToString()
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NOTIF-ERROR] Failed to send reschedule notification to old doctor for appointment {AppointmentId}", newAppointment.AppointmentId);
            }
        }

        return ToAppointmentResponse(newAppointment);
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
