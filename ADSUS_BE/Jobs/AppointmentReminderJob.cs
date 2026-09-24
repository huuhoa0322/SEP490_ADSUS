using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ADSUS_BE.Jobs;

/// <summary>
/// JOB-03 — Nhắc nhở lịch khám trước 24 giờ.
/// Chạy mỗi giờ.
/// GB-08: chỉ gửi push notification, không gửi Email/SMS.
/// </summary>
[DisallowConcurrentExecution]
public sealed class AppointmentReminderJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly ILogger<AppointmentReminderJob> _logger;

    /// <summary>Độ dài cửa sổ nhắc: 24 giờ trước.</summary>
    private const int ReminderWindowHours = 24;

    /// <summary>Ngưỡng dưới: nhắc khi còn 20-24 giờ.</summary>
    private const int MinHoursBefore = 20;

    public AppointmentReminderJob(
        IServiceScopeFactory scopeFactory,
        IAppointmentRepository appointmentRepo,
        ILogger<AppointmentReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _appointmentRepo = appointmentRepo;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // Tạo scope mới cho scoped services
        using var scope = _scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        _logger.LogInformation("[JOB-03] Appointment reminder job started at {Time}", now);

        // SlotDate/StartTime là giờ địa phương phòng khám (UTC+7, xem ClinicClock) — quy cửa sổ
        // "còn 20-24 giờ" về cùng hệ giờ đó rồi lọc ngay trong SQL. Bản trước nạp mọi bệnh nhân
        // rồi truy vấn lịch hẹn từng người (N+1, P11 review 24/09/2026), lại so giờ địa phương
        // với UTC nên nhắc lệch 7 tiếng và in sai giờ khám trong nội dung.
        var nowLocal = now + ClinicClock.Offset;
        var appointments = await _appointmentRepo.ListBookedStartingBetweenAsync(
            nowLocal.AddHours(MinHoursBefore),
            nowLocal.AddHours(ReminderWindowHours),
            context.CancellationToken);
        _logger.LogInformation("[JOB-03] Found {Count} appointments to remind", appointments.Count);

        var sentCount = 0;

        foreach (var ap in appointments)
        {
            try
            {
                // Lịch đặt hộ nhắc người đặt; tự đặt nhắc chủ hồ sơ. Hồ sơ guest (người thân chưa
                // có tài khoản) không có UserId nên chỉ người đặt hộ nhận được nhắc.
                var targetUserId = ap.BookedByUserId ?? ap.PatientProfile?.UserId;
                if (targetUserId is null || targetUserId == Guid.Empty) continue;

                var doctorName = ap.Slot?.Doctor?.FullName ?? "bác sĩ";
                var slotTimeLocal = new DateTimeOffset(
                    ap.Slot!.SlotDate.ToDateTime(ap.Slot.StartTime),
                    ClinicClock.Offset);

                await notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = targetUserId.Value,
                    Type = "appointment_reminder",
                    Title = "Nhắc lịch khám",
                    Body = $"Ngày mai bạn có lịch khám với {doctorName} lúc {slotTimeLocal:HH:mm}.",
                    Metadata = new Dictionary<string, object>
                    {
                        ["appointmentId"] = ap.AppointmentId.ToString(),
                        ["doctorName"] = doctorName,
                        ["slotTime"] = slotTimeLocal.ToString("O")
                    }
                }, context.CancellationToken);

                sentCount++;
                _logger.LogInformation(
                    "[JOB-03] Sent reminder for appointment {AppointmentId} to user {UserId}",
                    ap.AppointmentId, targetUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JOB-03] Failed to send reminder for appointment {AppointmentId}", ap.AppointmentId);
            }
        }

        _logger.LogInformation("[JOB-03] Appointment reminder job completed. Sent {Count} reminders", sentCount);
    }
}
