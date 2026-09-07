using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.Common.Settings;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace ADSUS_BE.Jobs;

/// <summary>
/// JOB-08 — Auto-cancel no-show appointments.
/// Chạy mỗi phút.
///
/// Business rule:
/// - Nếu appointment đang BOOKED và đã quá grace time (mặc định 15 phút) từ giờ bắt đầu khám
///   mà bệnh nhân không đến checkin → Tự động chuyển status: BOOKED → NO_SHOW
/// - Slot được giải phóng để bác sĩ biết slot trống
///
/// Ví dụ:
/// - Slot bắt đầu: 8:00, grace time: 15 phút
/// - Nếu 8:16 mà chưa checkin → Auto-NoShow
/// </summary>
[DisallowConcurrentExecution]
public sealed class NoShowCancellationJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly INotificationService _notificationService;
    private readonly IOptions<NoShowSettings> _noShowSettings;
    private readonly ILogger<NoShowCancellationJob> _logger;

    public NoShowCancellationJob(
        IServiceScopeFactory scopeFactory,
        INotificationService notificationService,
        IOptions<NoShowSettings> noShowSettings,
        ILogger<NoShowCancellationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _notificationService = notificationService;
        _noShowSettings = noShowSettings;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTime.UtcNow;
        var graceTimeMinutes = _noShowSettings.Value.GraceTimeMinutes;
        var thresholdTime = now.AddMinutes(-graceTimeMinutes);

        _logger.LogInformation("[JOB-08] No-show cancellation job started at {Time}, grace time: {GraceTime} minutes", now, graceTimeMinutes);

        // Tìm tất cả appointment đang BOOKED mà đã quá ngưỡng thời gian
        var noShowAppointments = await db.Appointments
            .Include(a => a.Slot)
                .ThenInclude(s => s.Doctor)
            .Include(a => a.PatientProfile)
                .ThenInclude(p => p.User)
            .Where(a => a.Status == AppointmentStatus.Booked)
            .Where(a => a.Slot != null)
            .ToListAsync(context.CancellationToken);

        var cancelledCount = 0;

        foreach (var appointment in noShowAppointments)
        {
            if (appointment.Slot == null) continue;

            var slotDateTime = appointment.Slot.SlotDate.ToDateTime(appointment.Slot.StartTime);

            // Kiểm tra nếu đã quá ngưỡng thời gian
            if (slotDateTime < thresholdTime)
            {
                try
                {
                    // Chuyển status: BOOKED → NO_SHOW
                    appointment.Status = AppointmentStatus.NoShow;
                    appointment.CancelledReason = $"Bệnh nhân không đến checkin trong vòng {graceTimeMinutes} phút.";
                    appointment.UpdatedAt = DateTime.UtcNow;

                    // Giải phóng slot (chuyển về OPEN)
                    appointment.Slot.Status = SlotStatus.Open;

                    // Gửi notification cho bệnh nhân
                    var doctorName = appointment.Slot?.Doctor?.FullName ?? "bác sĩ";
                    var slotTimeLocal = TimeZoneInfo.ConvertTimeFromUtc(
                        slotDateTime,
                        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));

                    try
                    {
                        var patientUserId = appointment.PatientProfile?.UserId;
                        if (patientUserId.HasValue)
                        {
                            await _notificationService.SendAsync(new SendNotificationRequest
                            {
                                UserId = patientUserId.Value,
                                Type = "no_show_cancelled",
                                Title = "Lịch hẹn bị hủy",
                                Body = $"Lịch khám với {doctorName} lúc {slotTimeLocal:HH:mm} ngày {slotTimeLocal:dd/MM/yyyy} đã bị hủy do bạn không đến checkin trong vòng {graceTimeMinutes} phút. Vui lòng đặt lịch khám mới.",
                                Metadata = new Dictionary<string, object>
                                {
                                    ["appointmentId"] = appointment.AppointmentId.ToString(),
                                    ["doctorName"] = doctorName,
                                    ["slotTime"] = slotTimeLocal.ToString("O"),
                                    ["reason"] = "no_show"
                                }
                            }, context.CancellationToken);
                        }
                    }
                    catch (Exception notifEx)
                    {
                        _logger.LogWarning(notifEx, "[JOB-08] Failed to send cancellation notification for appointment {AppointmentId}", appointment.AppointmentId);
                    }

                    cancelledCount++;
                    _logger.LogInformation(
                        "[JOB-08] Marked no-show appointment {AppointmentId}. Doctor: {DoctorName}, Slot: {SlotDate} {SlotTime}",
                        appointment.AppointmentId, doctorName, appointment.Slot?.SlotDate, appointment.Slot?.StartTime);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[JOB-08] Failed to process appointment {AppointmentId}", appointment.AppointmentId);
                }
            }
        }

        if (cancelledCount > 0)
        {
            await db.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("[JOB-08] Saved {Count} no-show appointments", cancelledCount);
        }

        _logger.LogInformation("[JOB-08] No-show cancellation job completed. No-show: {NoShow}", cancelledCount);
    }
}
