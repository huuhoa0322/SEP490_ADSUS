using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADSUS_BE.BLL.AppointmentScheduling.Services;

/// <summary>
/// Xử lý No-Show cho các appointment đã quá grace time.
/// </summary>
public sealed class NoShowService
{
    private readonly AppDbContext _db;
    private readonly NoShowSettings _settings;
    private readonly INotificationService _notificationService;
    private readonly IPatientProfileRepository _profileRepo;
    private readonly ILogger<NoShowService> _logger;

    public NoShowService(
        AppDbContext db,
        IOptions<NoShowSettings> settings,
        INotificationService notificationService,
        IPatientProfileRepository profileRepo,
        ILogger<NoShowService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _notificationService = notificationService;
        _profileRepo = profileRepo;
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

        // Đánh dấu No-Show
        appointment.Status = AppointmentStatus.NoShow;
        appointment.UpdatedAt = now;
        appointment.CancelledReason = $"Tự động hủy do không check-in trong {_settings.GraceTimeMinutes} phút kể từ lịch hẹn";

        await _db.SaveChangesAsync(ct);

        // Gửi notification cho patient khi bị No-Show
        try
        {
            var patientProfile = await _profileRepo.GetByIdAsync(appointment.PatientProfileId, ct);
            if (patientProfile != null)
            {
                await _notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patientProfile.UserId,
                    Type = "appointment_no_show",
                    Title = "Lịch khám đã bị hủy (No-Show)",
                    Body = $"Lịch khám ngày {appointment.Slot.SlotDate:dd/MM/yyyy} lúc {appointment.Slot.StartTime} đã bị hủy do không check-in trong {_settings.GraceTimeMinutes} phút.",
                    DeepLink = $"/appointments",
                    Metadata = new Dictionary<string, object>
                    {
                        ["appointmentId"] = appointment.AppointmentId.ToString()
                    }
                }, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send no-show notification to patient for appointment {AppointmentId}", appointment.AppointmentId);
        }

        // Gửi notification cho doctor khi có No-Show
        try
        {
            await _notificationService.SendAsync(new SendNotificationRequest
            {
                UserId = appointment.Slot.DoctorId,
                Type = "patient_no_show",
                Title = "Bệnh nhân không đến khám (No-Show)",
                Body = $"Bệnh nhân không check-in trong {_settings.GraceTimeMinutes} phút và đã được đánh dấu No-Show.",
                DeepLink = $"/appointments",
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

        return new NoShowResult
        {
            WasProcessed = true,
            PreviousStatus = AppointmentStatus.Booked,
            MinutesPastStart = (int)minutesPastStart
        };
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
