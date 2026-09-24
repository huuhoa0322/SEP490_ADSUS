using ADSUS_BE.BLL.AppointmentScheduling.DTOs;
using ADSUS_BE.BLL.AppointmentScheduling.Interfaces;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.MedicalRecord.DTOs;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.BLL.PatientRelationship.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

/// <summary>
/// Implementation của IAppointmentService (Module 8 — UC-13, UC-14).
/// Hỗ trợ đặt hộ cho người thân qua RelationshipId.
/// </summary>
public sealed class AppointmentService : IAppointmentService
{
    // Giới hạn đặt hộ: tối đa 3 appointment đang active đặt hộ
    private const int MaxActiveBookedForOthers = 3;
    // Giới hạn đặt cho bản thân: tối đa 3 appointment đang active
    private const int MaxActiveSelfBookings = 3;
    // Giới hạn cho mỗi hồ sơ bệnh nhân: tối đa 3 appointment đang active trên toàn hệ thống
    private const int MaxActivePerPatientProfile = 3;
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly IScheduleSlotRepository _slotRepo;
    private readonly IUserRepository _users;
    private readonly IPatientProfileService _patientProfiles;
    private readonly IPatientRelationshipService _relationships;
    private readonly INotificationService _notificationService;
    private readonly ICaseService _caseService;
    private readonly NoShowService _noShowService;
    private readonly AppDbContext _db;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(
        IAppointmentRepository appointmentRepo,
        IScheduleSlotRepository slotRepo,
        IUserRepository users,
        IPatientProfileService patientProfiles,
        IPatientRelationshipService relationships,
        INotificationService notificationService,
        ICaseService caseService,
        NoShowService noShowService,
        AppDbContext db,
        ILogger<AppointmentService> logger)
    {
        _appointmentRepo = appointmentRepo;
        _slotRepo = slotRepo;
        _users = users;
        _patientProfiles = patientProfiles;
        _relationships = relationships;
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

        // Chỉ trả về slot của bác sĩ ACTIVE, loại slot đã có appointment BOOKED hoặc Case InProgress,
        // và ẩn toàn bộ các slot trong quá khứ (SlotDate < today hoặc SlotDate == today && StartTime <= currentTime).
        var slots = rangeSlots
            .Where(s => s.Doctor.Status == UserStatus.Active)
            .Where(s => !s.Appointments.Any(a =>
                a.Status == AppointmentStatus.Booked
                || (a.Case != null && a.Case.Status == CaseStatus.InProgress)))
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
        Guid? userId = null,
        AppointmentStatus? statusFilter = null,
        CancellationToken ct = default)
    {
        var appointments = await _appointmentRepo.ListForPatientOrBookerAsync(patientProfileId, userId, statusFilter, ct);

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
            PatientFullName = a.PatientProfile?.User?.FullName ?? a.PatientProfile?.FullName ?? string.Empty,
            PatientPhone = a.PatientProfile?.User?.Phone ?? a.PatientProfile?.Phone,
            PatientProfileId = a.PatientProfileId,
            IsBookedForOthers = a.BookedByUserId != null,
            RelationshipLabel = a.PatientRelationship?.RelationshipName,
            BookedByUserName = a.BookedByUser?.FullName,
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

    public async Task<AppointmentResponse?> GetByIdAsync(
        Guid appointmentId,
        Guid currentUserId,
        string currentUserRole,
        Guid? currentPatientProfileId = null,
        CancellationToken ct = default)
    {
        var appointment = await _appointmentRepo.GetByIdAsync(appointmentId, ct);
        if (appointment == null) return null;

        // BR-065: Quyền xem chi tiết lịch hẹn:
        // 1. Admin được phép xem tất cả lịch hẹn
        var isAdmin = string.Equals(currentUserRole, "ADMIN", StringComparison.OrdinalIgnoreCase);
        if (isAdmin)
        {
            return ToAppointmentResponse(appointment);
        }

        // 2. Bác sĩ phụ trách lịch hẹn (khớp DoctorId của Slot hoặc Doctor User)
        var isDoctor = string.Equals(currentUserRole, "DOCTOR", StringComparison.OrdinalIgnoreCase);
        var isAssignedDoctor = appointment.Slot != null &&
            (appointment.Slot.DoctorId == currentUserId || (appointment.Slot.Doctor != null && appointment.Slot.Doctor.UserId == currentUserId));

        if (isDoctor && isAssignedDoctor)
        {
            return ToAppointmentResponse(appointment);
        }

        // 3. Bệnh nhân của lịch hẹn (chính chủ qua UserId, PatientProfileId, hoặc người thân đặt hộ qua BookedByUserId)
        var isPatient = string.Equals(currentUserRole, "PATIENT", StringComparison.OrdinalIgnoreCase);
        var isAssignedPatient =
            (appointment.PatientProfile != null && appointment.PatientProfile.UserId == currentUserId) ||
            (appointment.BookedByUserId.HasValue && appointment.BookedByUserId.Value == currentUserId) ||
            (currentPatientProfileId.HasValue && appointment.PatientProfileId == currentPatientProfileId.Value);

        if (isPatient && isAssignedPatient)
        {
            return ToAppointmentResponse(appointment);
        }

        // Nếu khớp trực tiếp phân quyền bác sĩ hoặc bệnh nhân kể cả khi role không trùng khớp hoàn toàn
        if (isAssignedDoctor || isAssignedPatient)
        {
            return ToAppointmentResponse(appointment);
        }

        throw new UnauthorizedAccessException("Bạn không có quyền truy cập thông tin lịch hẹn này.");
    }

    public Task<AppointmentResponse> BookAppointmentAsync(
        Guid patientProfileId,
        BookAppointmentRequest request,
        CancellationToken ct = default)
        => BookAppointmentAsync(patientProfileId, patientProfileId, request, isStaffOverride: true, ct);

    public async Task<AppointmentResponse> BookAppointmentAsync(
        Guid userId,
        Guid patientProfileId,
        BookAppointmentRequest request,
        bool isStaffOverride = false,
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

        // BR-02: Kiểm tra không trùng booking hoặc ca đang khám
        var isSlotBusy = slot.Appointments.Any(a =>
            a.Status == AppointmentStatus.Booked
            || (a.Case != null && a.Case.Status == CaseStatus.InProgress));
        if (isSlotBusy)
        {
            throw new InvalidOperationException("Slot này đã có người đặt hoặc đang có ca khám.");
        }

        // Không cho phép đặt lịch vào khung giờ đã qua (với quy tắc 5 phút cho Staff)
        var nowVn = GetNowVietnam();
        var todayVn = DateOnly.FromDateTime(nowVn);
        var currentTimeVn = TimeOnly.FromDateTime(nowVn);

        if (slot.SlotDate < todayVn)
        {
            throw new InvalidOperationException("Không thể đặt lịch vào ngày đã qua.");
        }

        if (slot.SlotDate == todayVn && slot.StartTime <= currentTimeVn)
        {
            if (!isStaffOverride)
            {
                throw new InvalidOperationException("Không thể đặt lịch vào khung giờ đã qua.");
            }

            // Khách vãng lai tại quầy (Staff):
            var minutesElapsed = (currentTimeVn - slot.StartTime).TotalMinutes;
            if (minutesElapsed > 5)
            {
                throw new InvalidOperationException(
                    "Đã quá 5 phút kể từ đầu ca. Vui lòng đặt vào slot tiếp theo rồi đẩy khám sớm.");
            }
        }

        // =====================================================
        // XÁC ĐỊNH ĐỐI TƯỢNG VÀ QUAN HỆ ĐẶT LỊCH
        // =====================================================
        var isBookingForOthers = request.RelationshipId.HasValue;
        Guid targetPatientProfileId;
        Guid? bookedByUserId = null;
        Guid? relationshipId = null;

        if (isBookingForOthers)
        {
            relationshipId = request.RelationshipId!.Value;

            // Staff đặt hộ: tìm quan hệ theo ID, không ép r.UserId == userId.
            // Bệnh nhân tự đặt: phải thuộc danh bạ của chính họ.
            var relationship = await _relationships.FindBookingTargetAsync(
                relationshipId.Value, isStaffOverride ? null : userId, ct);

            if (relationship == null)
            {
                throw new InvalidOperationException(
                    isStaffOverride ? "Không tìm thấy mối quan hệ này trong danh bạ." : "Không tìm thấy mối quan hệ này trong danh bạ của bạn.");
            }

            // Bệnh nhân thực sự được đặt khám
            targetPatientProfileId = relationship.PatientProfileId;
            bookedByUserId = isStaffOverride ? relationship.OwnerUserId : userId;

            // Pool 2: User đặt hộ tối đa 3 lịch active (BOOKED)
            if (!isStaffOverride)
            {
                var bookedForOthersActiveCount = await _appointmentRepo.CountBookedForOthersByUserAsync(userId, ct);

                if (bookedForOthersActiveCount >= MaxActiveBookedForOthers)
                {
                    throw new InvalidOperationException(
                        $"Bạn đã đặt tối đa {MaxActiveBookedForOthers} lịch hộ người thân đang chờ khám. Vui lòng hoàn thành hoặc hủy lịch cũ trước.");
                }
            }
        }
        else
        {
            // Tự đặt cho bản thân
            targetPatientProfileId = patientProfileId;

            // Pool 1: User tự đặt cho bản thân tối đa 3 lịch active (BOOKED)
            if (!isStaffOverride)
            {
                var selfActiveCount = await _appointmentRepo.CountSelfBookedByProfileAsync(targetPatientProfileId, ct);

                if (selfActiveCount >= MaxActiveSelfBookings)
                {
                    throw new InvalidOperationException(
                        $"Bạn đã có {MaxActiveSelfBookings} lịch hẹn đang chờ. Vui lòng hoàn thành hoặc hủy lịch cũ trước khi đặt mới.");
                }
            }
        }

        // Anti-Abuse Dual-Check: Không cho phép tự đặt lịch online nếu đã hủy >= 3 lần trong ngày hôm nay
        if (!isStaffOverride)
        {
            var todayStartUtc = ClinicClock.StartOfDayUtc(ClinicClock.Today());

            var userCancellationsToday = await _appointmentRepo.CountUserCancellationsSinceAsync(userId, todayStartUtc, ct);

            if (userCancellationsToday >= 3)
            {
                throw new InvalidOperationException(
                    "Bạn đã hủy lịch 3 lần trong ngày hôm nay. Quyền tự đặt lịch trực tuyến tạm thời bị khóa đến hết ngày. Vui lòng liên hệ hotline phòng khám để được hỗ trợ.");
            }

            var patientCancellationsToday = await _appointmentRepo.CountProfileCancellationsSinceAsync(targetPatientProfileId, todayStartUtc, ct);

            if (patientCancellationsToday >= 3)
            {
                throw new InvalidOperationException(
                    "Hồ sơ bệnh nhân này đã có 3 lần hủy lịch trong ngày hôm nay. Quyền tự đặt lịch trực tuyến cho hồ sơ này tạm thời bị khóa đến hết ngày. Vui lòng liên hệ hotline phòng khám để được hỗ trợ.");
            }
        }

        // =====================================================
        // POOL 3: QUY TẮC CỐT LÕI CHO PATIENT PROFILE (Tối đa 3 lịch active)
        // Bất kể ai đặt (chính bệnh nhân hay bất kỳ người thân nào đặt hộ),
        // 1 PatientProfile không bao giờ được có quá 3 lịch khám active cùng lúc!
        // =====================================================
        if (!isStaffOverride)
        {
            var patientTotalActiveCount = await _appointmentRepo.CountBookedByProfileAsync(targetPatientProfileId, ct);

            if (patientTotalActiveCount >= MaxActivePerPatientProfile)
            {
                throw new InvalidOperationException(
                    $"Bệnh nhân này đã có tối đa {MaxActivePerPatientProfile} lịch hẹn đang chờ khám trên hệ thống. Vui lòng hoàn thành hoặc hủy lịch cũ trước khi đặt mới.");
            }
        }

        // =====================================================
        // QUY TẮC TRONG NGÀY: 1 Bệnh nhân chỉ có tối đa 1 lịch active trong cùng 1 ngày khám
        // Áp dụng cho cả Bệnh nhân và Staff: 1 ngày chỉ được có tối đa 1 appointment = Booked!
        // =====================================================
        var existingAppointment = await _appointmentRepo.GetFirstOnDateAsync(
            targetPatientProfileId, slot.SlotDate, new[] { AppointmentStatus.Booked }, excludeAppointmentId: null, ct);

        if (existingAppointment != null)
        {
            var doctorName = existingAppointment.Slot?.Doctor?.FullName ?? "bác sĩ";
            throw new InvalidOperationException(
                $"Bệnh nhân đã có lịch khám với {doctorName} vào ngày {slot.SlotDate:dd/MM/yyyy}. " +
                "Mỗi ngày chỉ được đặt tối đa 1 lịch hẹn đang chờ khám (Booked). Vui lòng hủy lịch cũ trước khi đặt lịch mới.");
        }



        // =====================================================
        // TẠO APPOINTMENT
        // =====================================================
        var appointment = new Appointment
        {
            AppointmentId = Guid.NewGuid(),
            SlotId = request.ScheduleSlotId,
            PatientProfileId = targetPatientProfileId,
            Reason = request.Reason,
            Status = AppointmentStatus.Booked,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            BookedByUserId = bookedByUserId,
            RelationshipId = relationshipId,
        };

        // Luôn tạo Case khi đặt lịch cho đúng targetPatientProfileId
        var symptoms = request.Symptoms ?? new List<SymptomInput>();
        var caseId = await _caseService.CreateFromBookingAsync(
            targetPatientProfileId,
            slot.DoctorId,
            slot.SlotDate,
            symptoms,
            ct);

        appointment.CaseId = caseId;

        _logger.LogInformation(
            "Case {CaseId} created from appointment booking for appointment {AppointmentId}",
            caseId, appointment.AppointmentId);

        // Update slot status
        slot.Status = SlotStatus.Booked;
        slot.UpdatedAt = DateTime.UtcNow;

        await _appointmentRepo.CreateAsync(appointment, ct);
        await _slotRepo.UpdateAsync(slot, ct);

        // Load navigation properties for response
        appointment.Slot = slot;

        // Load BookedByUser and Relationship for response (if booking for others). Đọc bản
        // không-track và chỉ gắn vào đối tượng dựng response bên dưới — KHÔNG gắn vào lịch hẹn
        // đang được track, nếu không lần SaveChanges kế tiếp (vd khi gửi thông báo) sẽ hiểu nhầm
        // User/Relationship không-track đó là bản ghi mới cần INSERT.
        User? bookedByUser = null;
        ADSUS_BE.DAL.Entities.PatientRelationship? patientRelationship = null;
        if (bookedByUserId.HasValue || relationshipId.HasValue)
        {
            var loadedAppointment = await _appointmentRepo.GetWithBookerAndRelationshipAsync(appointment.AppointmentId, ct);
            bookedByUser = loadedAppointment?.BookedByUser;
            patientRelationship = loadedAppointment?.PatientRelationship;
        }

        var patientProfile = await _profileRepo.GetByIdAsync(targetPatientProfileId, ct);

        // Send notification to patient/booker (best effort - don't fail the booking if notification fails)
        try
        {
            // Hồ sơ guest có PatientUserId = Guid.Empty → không có ai để báo, giống bản cũ.
            var recipientUserId = bookedByUserId ?? (patientProfile?.PatientUserId ?? Guid.Empty);
            if (recipientUserId != Guid.Empty)
            {
                _logger.LogInformation(
                    "[NOTIF-DEBUG] Preparing to send booking notification to user {UserId} for appointment {AppointmentId}",
                    recipientUserId, appointment.AppointmentId);

                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = recipientUserId,
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
                    recipientUserId, appointment.AppointmentId);
            }
            else
            {
                _logger.LogWarning(
                    "[NOTIF-WARN] No recipient user id found for patientProfileId {PatientProfileId}",
                    targetPatientProfileId);
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
            var patientName = string.IsNullOrEmpty(patientProfile?.FullName) ? "Bệnh nhân" : patientProfile.FullName;
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

        var untrackedAppointment = new Appointment
        {
            AppointmentId = appointment.AppointmentId,
            SlotId = appointment.SlotId,
            PatientProfileId = appointment.PatientProfileId,
            Reason = appointment.Reason,
            Status = appointment.Status,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt,
            BookedByUserId = appointment.BookedByUserId,
            RelationshipId = appointment.RelationshipId,
            CaseId = appointment.CaseId,
            Slot = slot,
            BookedByUser = appointment.BookedByUser,
            PatientRelationship = appointment.PatientRelationship
        };

        if (untrackedAppointment.CaseId.HasValue)
        {
            untrackedAppointment.Case = await _db.Cases
                .Include(c => c.CaseSymptoms).ThenInclude(cs => cs.Category)
                .Include(c => c.CaseSymptoms).ThenInclude(cs => cs.Symptom)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CaseId == untrackedAppointment.CaseId.Value, ct);
        }

        var bookedSymptoms = untrackedAppointment.CaseId.HasValue
            ? await _caseService.ListSymptomsAsync(untrackedAppointment.CaseId.Value, ct)
            : null;

        return ToAppointmentResponse(untrackedAppointment, patient: patientProfile, symptoms: bookedSymptoms);
    }

    public async Task<AppointmentResponse> CreateFollowUpAppointmentAsync(
        Guid doctorId,
        FollowUpAppointmentRequest request,
        CancellationToken ct = default)
    {
        // 1. Kiểm tra slot tồn tại và lấy thông tin slot
        var slot = await _slotRepo.GetByIdForUpdateAsync(request.ScheduleSlotId, ct)
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
        var hasConflict = await _appointmentRepo.ExistsActiveInSlotAsync(request.PatientProfileId, request.ScheduleSlotId, ct);

        if (hasConflict)
        {
            throw new InvalidOperationException("Bệnh nhân đã có lịch hẹn trong khung giờ này.");
        }

        // [VÁ QA1-002]: Kiểm tra không có appointment Booked hoặc ca đang InProgress
        var isSlotBusy = slot.Appointments.Any(a =>
            a.Status == AppointmentStatus.Booked
            || (a.Case != null && a.Case.Status == CaseStatus.InProgress));
        if (isSlotBusy)
        {
            throw new InvalidOperationException("Khung giờ này hiện không khả dụng để đặt tái khám.");
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
        var patientProfile = await _patientProfiles.FindByIdAsync(request.PatientProfileId, ct);
        if (patientProfile == null)
        {
            throw new KeyNotFoundException("Không tìm thấy hồ sơ bệnh nhân.");
        }

        // 6. Validation: Max 3 active appointments cho bệnh nhân
        var activeAppointments = await _appointmentRepo.CountBookedByProfileAsync(request.PatientProfileId, ct);
        if (activeAppointments >= 3)
        {
            throw new InvalidOperationException(
                "Bệnh nhân đã có 3 lịch hẹn đang chờ. Vui lòng hoàn thành hoặc hủy lịch cũ trước khi đặt mới.");
        }

        // 7. Validation: 1 bệnh nhân - 1 ngày - tối đa 1 lịch
        var existingAppointment = await _appointmentRepo.GetFirstOnDateAsync(
            request.PatientProfileId, slot.SlotDate,
            new[] { AppointmentStatus.Booked, AppointmentStatus.Completed }, excludeAppointmentId: null, ct);
        if (existingAppointment != null)
        {
            var existingDoctorName = existingAppointment.Slot?.Doctor?.FullName ?? "bác sĩ";
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

        // Luôn tạo Case khi bác sĩ hẹn tái khám (mặc định không có triệu chứng ban đầu)
        var caseId = await _caseService.CreateFromBookingAsync(
            request.PatientProfileId,
            slot.DoctorId,
            slot.SlotDate,
            new List<SymptomInput>(),
            ct);

        appointment.CaseId = caseId;

        _logger.LogInformation(
            "Case {CaseId} created from doctor follow-up appointment {AppointmentId}",
            caseId, appointment.AppointmentId);

        slot.Status = SlotStatus.Booked;
        slot.UpdatedAt = DateTime.UtcNow;

        await _appointmentRepo.CreateAsync(appointment, ct);
        await _slotRepo.UpdateAsync(slot, ct);
        
        // Notify patient
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = patientProfile.PatientUserId,
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
            var patientName = string.IsNullOrEmpty(patientProfile.FullName) ? "bệnh nhân" : patientProfile.FullName;
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

        // Provide necessary nav props for response without mutating the tracked entity
        var untrackedAppointment = new Appointment
        {
            AppointmentId = appointment.AppointmentId,
            SlotId = appointment.SlotId,
            PatientProfileId = appointment.PatientProfileId,
            Reason = appointment.Reason,
            Status = appointment.Status,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt,
            CaseId = appointment.CaseId,
            Slot = slot
        };

        if (untrackedAppointment.CaseId.HasValue)
        {
            untrackedAppointment.Case = await _db.Cases
                .Include(c => c.CaseSymptoms).ThenInclude(cs => cs.Category)
                .Include(c => c.CaseSymptoms).ThenInclude(cs => cs.Symptom)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CaseId == untrackedAppointment.CaseId.Value, ct);
        }

        var followUpSymptoms = untrackedAppointment.CaseId.HasValue
            ? await _caseService.ListSymptomsAsync(untrackedAppointment.CaseId.Value, ct)
            : null;
        return ToAppointmentResponse(untrackedAppointment, patient: patientProfile, symptoms: followUpSymptoms);
    }

    public async Task<AppointmentResponse> CancelAppointmentAsync(
        Guid appointmentId,
        Guid userId,
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
        var appointment = await _appointmentRepo.GetByIdForUpdateAsync(appointmentId, ct)
            ?? throw new InvalidOperationException($"Appointment '{appointmentId}' not found.");

        // BR-01: Chỉ patient sở hữu HOẶC người đặt hộ mới được hủy
        if (appointment.PatientProfileId != patientProfileId && appointment.BookedByUserId != userId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền hủy lịch hẹn này.");
        }

        // BR-01: Chỉ BOOKED mới được hủy
        if (appointment.Status != AppointmentStatus.Booked)
        {
            throw new InvalidOperationException("Chỉ lịch hẹn đang đặt mới được hủy.");
        }

        // BR-057: Phải hủy lịch ít nhất trước 12 giờ
        var nowVn = GetNowVietnam();
        if (appointment.Slot != null)
        {
            var startDateTime = appointment.Slot.SlotDate.ToDateTime(appointment.Slot.StartTime);
            if (nowVn >= startDateTime.AddHours(-12))
            {
                throw new InvalidOperationException("Chỉ có thể hủy lịch ít nhất 12 giờ trước thời gian bắt đầu ca khám.");
            }
        }

        // Update appointment
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledReason = request.CancellationReason;
        appointment.UpdatedAt = DateTime.UtcNow;

        // Cập nhật Case status nếu có liên kết (Case thuộc module MedicalRecord — đi qua
        // ICaseService; thay đổi được lưu chung ở SaveChanges ngay dưới)
        if (appointment.CaseId.HasValue)
        {
            await _caseService.StageCancelFromAppointmentAsync(appointment.CaseId.Value, ct);
        }

        // Update slot status về OPEN
        var slot = appointment.Slot;
        slot.Status = SlotStatus.Open;
        slot.UpdatedAt = DateTime.UtcNow;

        await _appointmentRepo.SaveChangesAsync(ct);

        var patientProfile = await _profileRepo.GetByIdAsync(appointment.PatientProfileId, ct);

        // Send notification to patient about cancellation (best effort - don't fail cancellation if notification fails)
        try
        {
            // Hồ sơ guest có PatientUserId = Guid.Empty → không có ai để báo, giống bản cũ.
            var recipientUserId = appointment.BookedByUserId ?? patientProfile?.PatientUserId ?? Guid.Empty;
            if (recipientUserId != Guid.Empty)
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = recipientUserId,
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
            var patientName = string.IsNullOrEmpty(patientProfile?.FullName) ? "bệnh nhân" : patientProfile.FullName;
            
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

        var untrackedAppointment = new Appointment
        {
            AppointmentId = appointment.AppointmentId,
            SlotId = appointment.SlotId,
            PatientProfileId = appointment.PatientProfileId,
            Reason = appointment.Reason,
            Status = appointment.Status,
            CreatedAt = appointment.CreatedAt,
            UpdatedAt = appointment.UpdatedAt,
            CancelledReason = appointment.CancelledReason,
            BookedByUserId = appointment.BookedByUserId,
            RelationshipId = appointment.RelationshipId,
            CaseId = appointment.CaseId,
            Slot = slot,
            BookedByUser = appointment.BookedByUser,
            PatientRelationship = appointment.PatientRelationship,
            Case = appointment.Case
        };

        return ToAppointmentResponse(untrackedAppointment, patient: patientProfile);
    }

    public async Task<AppointmentResponse> CheckinAppointmentAsync(
        Guid appointmentId,
        CancellationToken ct = default)
    {
        // Lấy appointment với tracking để update
        var appointment = await _appointmentRepo.GetByIdForUpdateAsync(appointmentId, ct)
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

        // Cập nhật Case status nếu có liên kết (Case thuộc module MedicalRecord — đi qua
        // ICaseService; thay đổi được lưu chung ở SaveChanges ngay dưới)
        if (appointment.CaseId.HasValue)
        {
            await _caseService.StageCheckinFromAppointmentAsync(appointment.CaseId.Value, ct);
        }

        await _appointmentRepo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Appointment {AppointmentId} checked in by nurse. Status: {Status}",
            appointmentId, appointment.Status);

        var patientProfile = await _patientProfiles.FindByIdAsync(appointment.PatientProfileId, ct);
        // Hồ sơ guest (người thân chưa có tài khoản) có PatientUserId = Guid.Empty — giữ nguyên
        // hành vi cũ: chỉ lấy tên khi hồ sơ gắn với một tài khoản.
        var patientName = patientProfile is not null && patientProfile.PatientUserId != Guid.Empty
            ? patientProfile.FullName
            : "Bệnh nhân";

        // Gửi notification cho patient khi checkin thành công
        try
        {
            if (patientProfile != null)
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientProfile.PatientUserId,
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

        // Gửi notification cho doctor khi patient checkin (dẫn thẳng vào ca khám)
        try
        {
            var doctorDeepLink = appointment.CaseId.HasValue
                ? $"/cases/{appointment.CaseId.Value}"
                : $"/appointments/{appointment.AppointmentId}";

            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = appointment.Slot.DoctorId,
                Type = "patient_checked_in",
                Title = "Bệnh nhân đã tới khám",
                Body = $"Bệnh nhân {patientName} đã tới khám cho lịch hẹn ngày {appointment.Slot.SlotDate:dd/MM/yyyy} lúc {appointment.Slot.StartTime}.",
                DeepLink = doctorDeepLink,
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString(),
                    ["caseId"] = appointment.CaseId?.ToString() ?? string.Empty
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
        var appointment = await _appointmentRepo.GetLatestBookedByCaseForUpdateAsync(caseId, ct);

        if (appointment == null)
        {
            var existingAppt = await _appointmentRepo.GetLatestByCaseAsync(caseId, ct)
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

        // Cập nhật Case status nếu có liên kết (Case thuộc module MedicalRecord — đi qua
        // ICaseService; thay đổi được lưu chung ở SaveChanges ngay dưới)
        if (appointment.CaseId.HasValue)
        {
            await _caseService.StageCheckinFromAppointmentAsync(appointment.CaseId.Value, ct);
        }

        await _appointmentRepo.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Appointment {AppointmentId} checked in by nurse via CaseId {CaseId}. Status: {Status}",
            appointment.AppointmentId, caseId, appointment.Status);

        var patientProfile = await _patientProfiles.FindByIdAsync(appointment.PatientProfileId, ct);
        // Hồ sơ guest (người thân chưa có tài khoản) có PatientUserId = Guid.Empty — giữ nguyên
        // hành vi cũ: chỉ lấy tên khi hồ sơ gắn với một tài khoản.
        var patientName = patientProfile is not null && patientProfile.PatientUserId != Guid.Empty
            ? patientProfile.FullName
            : "Bệnh nhân";

        // Gửi notification cho patient khi checkin thành công
        try
        {
            if (patientProfile != null)
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientProfile.PatientUserId,
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

        // Gửi notification cho doctor khi patient checkin (dẫn thẳng vào ca khám)
        try
        {
            var doctorDeepLink = appointment.CaseId.HasValue
                ? $"/cases/{appointment.CaseId.Value}"
                : $"/cases/{caseId}";

            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = appointment.Slot.DoctorId,
                Type = "patient_checked_in",
                Title = "Bệnh nhân đã tới khám",
                Body = $"Bệnh nhân {patientName} đã tới khám cho lịch hẹn ngày {appointment.Slot.SlotDate:dd/MM/yyyy} lúc {appointment.Slot.StartTime}.",
                DeepLink = doctorDeepLink,
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString(),
                    ["caseId"] = (appointment.CaseId ?? caseId).ToString()
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
                PatientFullName = a.PatientProfile.User?.FullName ?? a.PatientProfile.FullName ?? "Bệnh nhân",
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
        var effectiveFrom = fromDate ?? ClinicClock.Today();
        var effectiveTo = toDate ?? effectiveFrom;
        if (effectiveFrom > effectiveTo)
        {
            (effectiveFrom, effectiveTo) = (effectiveTo, effectiveFrom);
        }

        // Filter theo status: ALL (mặc định không hiện ca hủy), BOOKED, APPROVED (Đã check-in), NOSHOW (Vắng mặt), CANCELLED (Đã hủy)
        AppointmentStatus? statusFilter = null;
        var excludeCancelled = false;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = status.Trim().Replace("_", "");
            if (string.Equals(normalized, "APPROVED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "CHECKEDIN", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "COMPLETED", StringComparison.OrdinalIgnoreCase))
            {
                // Bệnh nhân check-in trong DB mang trạng thái COMPLETED
                statusFilter = AppointmentStatus.Completed;
            }
            else if (string.Equals(normalized, "CANCELLED", StringComparison.OrdinalIgnoreCase))
            {
                statusFilter = AppointmentStatus.Cancelled;
            }
            else if (string.Equals(normalized, "NOSHOW", StringComparison.OrdinalIgnoreCase))
            {
                statusFilter = AppointmentStatus.NoShow;
            }
            else if (Enum.TryParse<AppointmentStatus>(normalized, true, out var parsedStatus))
            {
                statusFilter = parsedStatus;
            }
            // Giá trị lạ: không lọc gì (kể cả ca huỷ) — giữ nguyên hành vi cũ.
        }
        else
        {
            // Màn hình mặc định (ALL hoặc không chọn category):
            // KHÔNG hiển thị những ca đã hủy (Cancelled), chỉ hiện ca Booked, Completed, NoShow.
            // Chỉ khi điều dưỡng chọn đích danh category 'Đã huỷ' (CANCELLED) thì mới hiển thị.
            excludeCancelled = true;
        }

        var effectivePage = page < 1 ? 1 : page;
        var effectivePageSize = pageSize is < 1 or > 1000 ? 15 : pageSize;

        var queuePage = await _appointmentRepo.GetCheckinQueuePageAsync(
            effectiveFrom, effectiveTo, search, statusFilter, excludeCancelled, effectivePage, effectivePageSize, ct);
        var appointments = queuePage.Items;

        var items = appointments.Select(a => new CheckinQueueItemResponse
        {
            AppointmentId = a.AppointmentId,
            SlotTime = a.Slot.SlotDate.ToDateTime(a.Slot.StartTime),
            PatientFullName = a.PatientProfile?.User?.FullName ?? a.PatientProfile?.FullName ?? string.Empty,
            PatientPhone = a.PatientProfile?.User?.Phone ?? a.PatientProfile?.Phone,
            PatientProfileId = a.PatientProfileId,
            CaseId = a.CaseId ?? Guid.Empty,
            Reason = a.Reason,
            DoctorId = a.Slot?.DoctorId ?? Guid.Empty,
            DoctorName = a.Slot?.Doctor?.FullName ?? string.Empty,
            Status = a.Status,
        }).ToList();

        return new CheckinQueueResponse
        {
            Items = items,
            Page = effectivePage,
            PageSize = effectivePageSize,
            TotalCount = queuePage.TotalCount,
            BookedCount = queuePage.BookedCount,
            CheckedInCount = queuePage.CheckedInCount,
            CancelledCount = queuePage.CancelledCount,
            NoShowCount = queuePage.NoShowCount,
        };
    }

    public async Task<AppointmentResponse> RescheduleAppointmentAsync(
        Guid oldAppointmentId,
        RescheduleAppointmentRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.RescheduleReason))
        {
            throw new InvalidOperationException("Lý do đổi lịch là bắt buộc.");
        }

        var oldAppointment = await _appointmentRepo.GetForRescheduleAsync(oldAppointmentId, ct)
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
        var newSlot = await _slotRepo.GetByIdForUpdateAsync(request.NewScheduleSlotId, ct)
            ?? throw new KeyNotFoundException($"Khung giờ '{request.NewScheduleSlotId}' không tồn tại.");

        if (newSlot.Status != SlotStatus.Open)
        {
            throw new InvalidOperationException("Khung giờ này không còn nhận đặt lịch.");
        }

        var isSlotBusy = newSlot.Appointments.Any(a =>
            a.Status == AppointmentStatus.Booked
            || (a.Case != null && a.Case.Status == CaseStatus.InProgress));
        if (isSlotBusy)
        {
            throw new InvalidOperationException("Khung giờ mới này đã có người đặt hoặc đang có ca khám.");
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
        var sameDayConflict = await _appointmentRepo.GetFirstOnDateAsync(
            oldAppointment.PatientProfileId, newSlot.SlotDate,
            new[] { AppointmentStatus.Booked, AppointmentStatus.Completed },
            excludeAppointmentId: oldAppointment.AppointmentId, ct);

        if (sameDayConflict != null)
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
            BookedByUserId = oldAppointment.BookedByUserId,
            RelationshipId = oldAppointment.RelationshipId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        // AppDbContext chỉ còn dùng để mở transaction bao ngoài (giống CaseDiagnosisService) — mọi
        // đọc/ghi entity đi qua Repository hoặc service của module sở hữu.
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
            }
            // Scenario 2 (COMPLETED) và 3 (CANCELLED / NO_SHOW): lịch hẹn cũ và slot cũ giữ nguyên.

            // Case thuộc module MedicalRecord — quyết định giá trị mới ở đây, ghi qua ICaseService
            // (không lưu riêng, lưu chung ở SaveChanges bên dưới trong cùng transaction).
            if (oldAppointment.Case != null)
            {
                var reassignDoctorTo = newSlot.DoctorId != oldAppointment.Slot?.DoctorId
                    ? newSlot.DoctorId
                    : (Guid?)null;

                CaseStatus? newStatusForCase = null;
                if (isScenario1)
                {
                    newStatusForCase = newCaseStatus;
                }
                else if (isScenario2)
                {
                    // Case remains InProgress (do not downgrade to Booked)
                    newStatusForCase = CaseStatus.InProgress;
                }
                else if (isScenario3 && oldAppointment.Case.Status == CaseStatus.Cancelled)
                {
                    newStatusForCase = newCaseStatus;
                }

                await _caseService.StageRescheduleFromAppointmentAsync(
                    oldAppointment.Case.CaseId, reassignDoctorTo, newStatusForCase, ct);
            }

            // New slot is booked
            newSlot.Status = SlotStatus.Booked;
            newSlot.UpdatedAt = now;

            // Add new appointment
            await _appointmentRepo.AddAsync(newAppointment, ct);

            await _appointmentRepo.SaveChangesAsync(ct);

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

        // We will create an untracked instance at the end for mapping, so do not mutate newAppointment here.
        var untrackedNewAppointment = new Appointment
        {
            AppointmentId = newAppointment.AppointmentId,
            SlotId = newAppointment.SlotId,
            PatientProfileId = newAppointment.PatientProfileId,
            Reason = newAppointment.Reason,
            Status = newAppointment.Status,
            CreatedAt = newAppointment.CreatedAt,
            UpdatedAt = newAppointment.UpdatedAt,
            BookedByUserId = newAppointment.BookedByUserId,
            RelationshipId = newAppointment.RelationshipId,
            CaseId = newAppointment.CaseId,
            Slot = newSlot,
            PatientProfile = oldAppointment.PatientProfile,
            BookedByUser = oldAppointment.BookedByUser,
            PatientRelationship = oldAppointment.PatientRelationship,
            Case = oldAppointment.Case
        };

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

        return ToAppointmentResponse(untrackedNewAppointment);
    }

    public async Task<CancellationStatusTodayResponse> GetCancellationStatusTodayAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var todayStartUtc = ClinicClock.StartOfDayUtc(ClinicClock.Today());

        var cancellationsToday = await _appointmentRepo.CountUserCancellationsSinceAsync(userId, todayStartUtc, ct);

        return new CancellationStatusTodayResponse
        {
            CancellationsToday = cancellationsToday,
            MaxCancellations = 3,
            CanBookOnline = cancellationsToday < 3
        };
    }

    public async Task<AppointmentResponse> UpdateClinicalInfoAsync(
        Guid appointmentId,
        Guid userId,
        Guid? callerPatientProfileId,
        UpdateAppointmentClinicalInfoRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var appointment = await _appointmentRepo.GetByIdWithDetailsForUpdateAsync(appointmentId, ct)
            ?? throw new KeyNotFoundException($"Không tìm thấy lịch hẹn '{appointmentId}'.");

        if (appointment.Status != AppointmentStatus.Booked)
        {
            throw new InvalidOperationException("Chỉ có thể chỉnh sửa thông tin cho lịch hẹn đang ở trạng thái ĐÃ ĐẶT.");
        }

        var nowVn = GetNowVietnam();
        var todayVn = DateOnly.FromDateTime(nowVn);
        var currentTimeVn = TimeOnly.FromDateTime(nowVn);
        if (appointment.Slot.SlotDate < todayVn || (appointment.Slot.SlotDate == todayVn && appointment.Slot.StartTime <= currentTimeVn))
        {
            throw new InvalidOperationException("Không thể chỉnh sửa thông tin cho lịch hẹn trong quá khứ hoặc đã đến giờ khám.");
        }

        // Phân quyền kép
        var hasPermission = appointment.BookedByUserId == userId
            || (appointment.PatientProfile?.UserId != null && appointment.PatientProfile.UserId == userId)
            || (callerPatientProfileId.HasValue && callerPatientProfileId.Value == appointment.PatientProfileId);

        if (!hasPermission)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền chỉnh sửa thông tin lịch hẹn này.");
        }

        if (request.Reason != null)
        {
            appointment.Reason = request.Reason;
        }

        var validSymptoms = request.Symptoms?
            .Where(s => s.CategoryId != Guid.Empty)
            .ToList() ?? new List<SymptomInput>();

        if (appointment.CaseId.HasValue)
        {
            // Case thuộc module MedicalRecord — thay triệu chứng qua ICaseService, lưu chung ở
            // SaveChanges bên dưới. request.Symptoms null = không đụng tới triệu chứng.
            await _caseService.StageReplaceSymptomsFromAppointmentAsync(
                appointment.CaseId.Value,
                request.Symptoms != null ? validSymptoms : null,
                ct);
        }
        else if (validSymptoms.Count > 0)
        {
            var newCaseId = await _caseService.CreateFromBookingAsync(
                appointment.PatientProfileId,
                appointment.Slot.DoctorId,
                appointment.Slot.SlotDate,
                validSymptoms,
                ct);
            appointment.CaseId = newCaseId;
        }

        appointment.UpdatedAt = DateTime.UtcNow;
        await _appointmentRepo.SaveChangesAsync(ct);

        var symptoms = appointment.CaseId.HasValue
            ? await _caseService.ListSymptomsAsync(appointment.CaseId.Value, ct)
            : null;

        return ToAppointmentResponse(appointment, symptoms: symptoms);
    }

    /// <param name="patient">
    /// Hồ sơ bệnh nhân lấy qua IPatientProfileService — truyền vào khi lịch hẹn KHÔNG được nạp kèm
    /// navigation PatientProfile. Không truyền thì đọc từ navigation như trước.
    /// </param>
    /// <param name="symptoms">
    /// Triệu chứng lấy qua ICaseService.ListSymptomsAsync — tương tự, thay cho navigation Case.
    /// </param>
    private static AppointmentResponse ToAppointmentResponse(
        Appointment a,
        Guid? caseId = null,
        PatientProfileResponse? patient = null,
        IReadOnlyList<CaseSymptomResponse>? symptoms = null)
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
            PatientFullName = patient != null
                ? patient.FullName
                : a.PatientProfile?.User?.FullName ?? a.PatientProfile?.FullName ?? string.Empty,
            PatientPhone = patient != null
                ? (string.IsNullOrEmpty(patient.Phone) ? null : patient.Phone)
                : a.PatientProfile?.User?.Phone ?? a.PatientProfile?.Phone,
            PatientProfileId = a.PatientProfileId,
            BookedByUserName = a.BookedByUser?.FullName,
            RelationshipLabel = a.PatientRelationship?.RelationshipName,
            IsBookedForOthers = a.BookedByUserId != null,
            Symptoms = symptoms != null
                ? symptoms.Select(cs => new AppointmentSymptomResponse
                {
                    CategoryId = cs.CategoryId,
                    CategoryName = cs.CategoryName,
                    SymptomId = cs.SymptomId,
                    SymptomName = cs.SymptomName,
                    OtherNote = cs.OtherNote
                }).ToList()
                : a.Case?.CaseSymptoms?.Select(cs => new AppointmentSymptomResponse
                {
                    CategoryId = cs.CategoryId,
                    CategoryName = cs.Category?.Name ?? string.Empty,
                    SymptomId = cs.SymptomId,
                    SymptomName = cs.Symptom?.Name,
                    OtherNote = cs.OtherNote
                }).ToList() ?? new List<AppointmentSymptomResponse>()
        };
    }

    public async Task ReadyForNextPatientAsync(Guid doctorId, CancellationToken ct = default)
    {
        var doctor = await _users.GetByIdReadOnlyAsync(doctorId, ct)
            ?? throw new InvalidOperationException("Doctor not found.");

        var nowVn = GetNowVietnam();
        var todayVn = DateOnly.FromDateTime(nowVn);

        // [VÁ QA2-001]: Kích hoạt quét và tái chế các slot kết thúc sớm của Bác sĩ này
        await RecycleCompletedEarlySlotsAsync(doctorId, ct);

        // Tìm ca tiếp theo trong ngày
        var nextAppointment = await _appointmentRepo.GetNextForDoctorOnDateAsync(doctorId, todayVn, ct);

        // Bắn SignalR notification tới Staff/Lễ tân
        var staffUsers = await _users.ListActiveUserIdsByRoleAsync(UserRole.Staff, ct);

        string title = "Bác sĩ sẵn sàng tiếp nhận";
        string body;
        var metadata = new Dictionary<string, object>
        {
            ["doctorId"] = doctorId.ToString(),
            ["doctorName"] = doctor.FullName,
        };

        if (nextAppointment != null)
        {
            var patientName = nextAppointment.PatientProfile?.User?.FullName
                ?? nextAppointment.PatientProfile?.FullName
                ?? "bệnh nhân";
            var slotTime = nextAppointment.Slot?.StartTime.ToString("HH:mm") ?? "";
            
            body = $"BS. {doctor.FullName} đã sẵn sàng tiếp nhận ca tiếp theo: {patientName} ({slotTime}). Mời bệnh nhân vào phòng khám.";
            metadata["patientName"] = patientName;
        }
        else
        {
            body = $"BS. {doctor.FullName} đã sẵn sàng tiếp nhận ca tiếp theo.";
        }

        foreach (var staffId in staffUsers)
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = staffId,
                Type = "doctor_ready_next",
                Title = title,
                Body = body,
                DeepLink = "/checkin",
                Metadata = metadata
            }, ct);
        }
    }

    public async Task RecycleCompletedEarlySlotsAsync(Guid doctorId, CancellationToken ct = default)
    {
        var nowVn = GetNowVietnam();
        var todayVn = DateOnly.FromDateTime(nowVn);
        var currentTimeVn = TimeOnly.FromDateTime(nowVn);

        var slotsToRecycle = await _slotRepo.ListBookedForDoctorAfterForUpdateAsync(doctorId, todayVn, currentTimeVn, ct);

        foreach (var slot in slotsToRecycle)
        {
            var hasActiveBooking = slot.Appointments.Any(a =>
                a.Status == AppointmentStatus.Booked);
            var hasActiveCaseInProgress = slot.Appointments.Any(a =>
                a.Case != null && a.Case.Status == CaseStatus.InProgress);

            if (!hasActiveBooking && !hasActiveCaseInProgress)
            {
                slot.Status = SlotStatus.Open;
                slot.UpdatedAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "Slot {SlotId} recycled to Open (doctor {DoctorId}, date {Date}, time {Time})",
                    slot.SlotId, doctorId, todayVn, slot.StartTime);
            }
        }

        await _slotRepo.SaveChangesAsync(ct);
    }
}
