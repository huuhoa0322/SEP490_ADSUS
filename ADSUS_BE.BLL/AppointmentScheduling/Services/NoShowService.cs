using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.BLL.MedicalRecord.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

/// <summary>
/// Xử lý No-Show cho các appointment đã quá grace time — nguồn DUY NHẤT cho cả check-in muộn
/// (AppointmentService) lẫn JOB-08 (NoShowCancellationJob). Trước 25/09/2026 job có bản logic
/// riêng lệch với service (điều kiện huỷ Case, Slot.UpdatedAt, lý do, người nhận thông báo) —
/// nay gộp về đây theo quyết định của người dùng (P11 review).
///
/// Quy tắc: lịch đang Booked, tính từ giờ BẮT ĐẦU của slot đã qua ít nhất GraceTimeMinutes (đúng
/// phút thứ 15 cũng tính) → NoShow, slot mở lại, Case liên kết còn Booked thì huỷ. Báo bệnh nhân
/// (hồ sơ guest chưa có tài khoản thì báo người đặt hộ) và bác sĩ.
/// </summary>
public sealed class NoShowService
{
    private readonly NoShowSettings _settings;
    private readonly IAppointmentRepository _appointments;
    private readonly ICaseService _cases;
    private readonly IPatientProfileService _patientProfiles;
    private readonly INotificationService _notificationService;
    private readonly ILogger<NoShowService> _logger;

    public NoShowService(
        IOptions<NoShowSettings> settings,
        IAppointmentRepository appointments,
        ICaseService cases,
        IPatientProfileService patientProfiles,
        INotificationService notificationService,
        ILogger<NoShowService> logger)
    {
        _settings = settings.Value;
        _appointments = appointments;
        _cases = cases;
        _patientProfiles = patientProfiles;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Kiểm tra và xử lý No-Show nếu đã quá grace time.
    /// Grace time tính từ START time của slot.
    /// Ví dụ: Slot 8:00, grace time 15 phút → hết grace lúc 8:15
    /// </summary>
    public async Task<NoShowResult> ProcessNoShowAsync(
        Appointment appointment,
        CancellationToken ct = default)
    {
        // Chỉ xử lý nếu status = Booked
        if (appointment.Status != AppointmentStatus.Booked)
        {
            return new NoShowResult { WasProcessed = false };
        }

        // Tính slot start time với timezone
        var slotStartDateTime = ClinicClock.StartOfDayUtc(appointment.Slot.SlotDate)
            .Add(appointment.Slot.StartTime.ToTimeSpan());
        var now = DateTime.UtcNow;
        var minutesPastStart = (now - slotStartDateTime).TotalMinutes;

        // Nếu chưa quá grace time → không xử lý
        if (minutesPastStart < _settings.GraceTimeMinutes)
        {
            return new NoShowResult
            {
                WasProcessed = false,
                MinutesPastStart = (int)minutesPastStart,
                RemainingMinutes = _settings.GraceTimeMinutes - (int)minutesPastStart
            };
        }

        await MarkSaveAndNotifyAsync(new[] { appointment }, now, ct);

        return new NoShowResult
        {
            WasProcessed = true,
            PreviousStatus = AppointmentStatus.Booked,
            MinutesPastStart = (int)minutesPastStart
        };
    }

    /// <summary>
    /// JOB-08 — đánh dấu No-Show mọi lịch Booked đã quá grace time: một truy vấn lấy lịch, một
    /// truy vấn Case, một lần lưu. Trả về số lịch đã đánh dấu.
    /// </summary>
    public async Task<int> ProcessOverdueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // SlotDate/StartTime là giờ địa phương phòng khám — quy ngưỡng về cùng hệ giờ để lọc
        // ngay trong SQL. Slot bắt đầu đúng tại ngưỡng cũng tính (giống ProcessNoShowAsync).
        var thresholdLocal = now.AddMinutes(-_settings.GraceTimeMinutes) + ClinicClock.Offset;
        var overdue = await _appointments.ListBookedStartedAtOrBeforeForUpdateAsync(thresholdLocal, ct);

        if (overdue.Count == 0)
            return 0;

        await MarkSaveAndNotifyAsync(overdue, now, ct);
        return overdue.Count;
    }

    private async Task MarkSaveAndNotifyAsync(
        IReadOnlyList<Appointment> appointments,
        DateTime now,
        CancellationToken ct)
    {
        foreach (var appointment in appointments)
        {
            appointment.Status = AppointmentStatus.NoShow;
            appointment.UpdatedAt = now;
            appointment.CancelledReason = $"Tự động hủy do không check-in trong {_settings.GraceTimeMinutes} phút kể từ lịch hẹn";

            // [VÁ QA2-002]: Giải phóng slot để không bị kẹt ở trạng thái Booked vĩnh viễn
            if (appointment.Slot != null)
            {
                appointment.Slot.Status = SlotStatus.Open;
                appointment.Slot.UpdatedAt = now;
            }
        }

        // Case thuộc module MedicalRecord — huỷ qua ICaseService (chỉ Case còn Booked), lưu chung
        // với lịch hẹn ở SaveChanges ngay dưới.
        var caseIds = appointments
            .Where(a => a.CaseId.HasValue)
            .Select(a => a.CaseId!.Value)
            .Distinct()
            .ToList();
        if (caseIds.Count > 0)
        {
            await _cases.StageNoShowFromAppointmentsAsync(caseIds, ct);
        }

        await _appointments.SaveChangesAsync(ct);

        // Thông báo lỗi không được làm hỏng việc đánh dấu No-Show đã lưu.
        IReadOnlyDictionary<Guid, Guid?> profileUserIds;
        try
        {
            profileUserIds = await _patientProfiles.FindUserIdsAsync(
                appointments.Select(a => a.PatientProfileId).Distinct().ToList(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load patient accounts for no-show notifications");
            profileUserIds = new Dictionary<Guid, Guid?>();
        }

        foreach (var appointment in appointments)
        {
            await NotifyAsync(appointment, profileUserIds, ct);
        }
    }

    private async Task NotifyAsync(
        Appointment appointment,
        IReadOnlyDictionary<Guid, Guid?> profileUserIds,
        CancellationToken ct)
    {
        var slot = appointment.Slot;
        var doctorName = slot?.Doctor?.FullName ?? "bác sĩ";
        var slotTimeStr = slot is null ? string.Empty : $"{slot.StartTime:HH\\:mm}";
        var slotDateStr = slot is null ? string.Empty : $"{slot.SlotDate:dd/MM/yyyy}";

        // Bệnh nhân có tài khoản nhận thông báo; hồ sơ guest (người thân chưa có tài khoản) thì
        // người đặt hộ nhận — cùng cách JOB-03 nhắc lịch.
        var patientUserId = profileUserIds.GetValueOrDefault(appointment.PatientProfileId);
        var recipientUserId = patientUserId is { } id && id != Guid.Empty ? id : appointment.BookedByUserId;

        if (recipientUserId is { } recipient && recipient != Guid.Empty)
        {
            try
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = recipient,
                    Type = "appointment_no_show",
                    Title = "Lịch khám đã bị hủy (No-Show)",
                    Body = $"Lịch khám với {doctorName} lúc {slotTimeStr} ngày {slotDateStr} đã bị hủy do không check-in trong {_settings.GraceTimeMinutes} phút. Vui lòng đặt lịch khám mới.",
                    DeepLink = "/appointments",
                    Metadata = new Dictionary<string, object>
                    {
                        ["appointmentId"] = appointment.AppointmentId.ToString(),
                        ["doctorName"] = doctorName,
                        ["slotTime"] = $"{slotDateStr} {slotTimeStr}",
                        ["reason"] = "no_show"
                    }
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send no-show notification to patient for appointment {AppointmentId}", appointment.AppointmentId);
            }
        }

        if (slot is null) return;

        // Gửi notification cho doctor khi có No-Show
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = slot.DoctorId,
                Type = "patient_no_show",
                Title = "Bệnh nhân không đến khám (No-Show)",
                Body = $"Bệnh nhân của lịch khám lúc {slotTimeStr} ngày {slotDateStr} không check-in trong {_settings.GraceTimeMinutes} phút và đã được đánh dấu No-Show.",
                DeepLink = "/appointments",
                Metadata = new Dictionary<string, object>
                {
                    ["appointmentId"] = appointment.AppointmentId.ToString()
                }
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send no-show notification to doctor for appointment {AppointmentId}", appointment.AppointmentId);
        }
    }
}

/// <summary>
/// Kết quả xử lý No-Show.
/// </summary>
public sealed class NoShowResult
{
    public bool WasProcessed { get; init; }
    public AppointmentStatus? PreviousStatus { get; init; }
    public int MinutesPastStart { get; init; }
    public int RemainingMinutes { get; init; }
}
