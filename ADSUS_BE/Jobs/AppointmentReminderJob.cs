using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

/// <summary>
/// JOB-03 — Nhắc nhở lịch khám trước 24 giờ.
/// Chạy mỗi giờ.
/// GB-08: chỉ gửi push notification, không gửi Email/SMS.
/// </summary>
[DisallowConcurrentExecution]
public sealed class AppointmentReminderJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPatientProfileRepository _patientProfileRepo;
    private readonly IAppointmentRepository _appointmentRepo;
    private readonly ILogger<AppointmentReminderJob> _logger;

    /// <summary>Độ dài cửa sổ nhắc: 24 giờ trước.</summary>
    private const int ReminderWindowHours = 24;

    /// <summary>Ngưỡng dưới: nhắc khi còn 20-24 giờ.</summary>
    private const int MinHoursBefore = 20;

    public AppointmentReminderJob(
        IServiceScopeFactory scopeFactory,
        IPatientProfileRepository patientProfileRepo,
        IAppointmentRepository appointmentRepo,
        ILogger<AppointmentReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _patientProfileRepo = patientProfileRepo;
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

        // Lấy tất cả bệnh nhân
        var patients = await _patientProfileRepo.SearchAsync(null, null, null, 1, int.MaxValue, context.CancellationToken);
        _logger.LogInformation("[JOB-03] Found {Count} patient profiles to check", patients.Items.Count);

        var sentCount = 0;

        foreach (var row in patients.Items)
        {
            // Bỏ qua dòng không có PatientProfile (chưa có hồ sơ nền)
            if (!row.PatientProfileId.HasValue) continue;

            try
            {
                // Lấy appointments của bệnh nhân
                var appointments = await _appointmentRepo.ListByPatientAsync(
                    row.PatientProfileId.Value,
                    context.CancellationToken);

                foreach (var ap in appointments)
                {
                    // Chỉ BOOKED appointments (đã đặt lịch và chưa bị hủy)
                    if (ap.Status != AppointmentStatus.Booked) continue;

                    // Tính thời gian đến giờ khám (Slot luôn được Include trong ListByPatientAsync)
                    var appointmentTime = ap.Slot!.SlotDate.ToDateTime(ap.Slot.StartTime);
                    var hoursUntil = (appointmentTime - now).TotalHours;

                    // Chỉ nhắc nếu trong khoảng 20-24 giờ
                    if (hoursUntil < MinHoursBefore || hoursUntil > ReminderWindowHours) continue;

                    // Gửi notification
                    var doctorName = ap.Slot?.Doctor?.FullName ?? "bác sĩ";
                    var slotTimeLocal = TimeZoneInfo.ConvertTimeFromUtc(
                        appointmentTime,
                        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));

                    await notificationService.SendAsync(new SendNotificationRequest
                    {
                        UserId = row.PatientUserId,
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
                        "[JOB-03] Sent reminder for appointment {AppointmentId} to user {UserId} ({Hours}h before)",
                        ap.AppointmentId, row.PatientUserId, (int)hoursUntil);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JOB-03] Failed to process patient profile {ProfileId}", row.PatientProfileId);
            }
        }

        _logger.LogInformation("[JOB-03] Appointment reminder job completed. Sent {Count} reminders", sentCount);
    }
}
