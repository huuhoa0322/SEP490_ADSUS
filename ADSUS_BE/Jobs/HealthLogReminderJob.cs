using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

/// <summary>
/// JOB-04 — Nhắc nhở ghi nhật ký sức khỏe.
/// Chạy 2 lần mỗi ngày: 8h sáng và 20h tối.
/// GB-08: chỉ gửi push notification, không gửi Email/SMS.
/// </summary>
[DisallowConcurrentExecution]
public sealed class HealthLogReminderJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<HealthLogReminderJob> _logger;

    public HealthLogReminderJob(
        IServiceScopeFactory scopeFactory,
        IUserRepository userRepo,
        ILogger<HealthLogReminderJob> logger)
    {
        _scopeFactory = scopeFactory;
        _userRepo = userRepo;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        // Tạo scope mới cho scoped services
        using var scope = _scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var now = DateTime.UtcNow;
        var hcmTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(now, hcmTimeZone);

        _logger.LogInformation("[JOB-04] Health log reminder job started at {Time} (local: {LocalTime})",
            now, localNow);

        // Xác định loại reminder: buổi sáng hay tối
        var isMorning = localNow.Hour < 12;
        var reminderType = isMorning ? "morning" : "evening";

        // Gửi notification cho tất cả bệnh nhân
        var patients = await _userRepo.GetAllPatientsAsync(context.CancellationToken);
        _logger.LogInformation("[JOB-04] Found {Count} patients for {Type} reminder",
            patients.Count, reminderType);

        var sentCount = 0;

        foreach (var patient in patients)
        {
            try
            {
                // Kiểm tra nếu đã nhắc hôm nay (tránh duplicate)
                // Chỉ check đơn giản - gửi notification, notification service sẽ xử lý
                var title = isMorning ? "Buổi sáng ghi nhật ký sức khỏe"
                                      : "Buổi tối ghi nhật ký sức khỏe";
                var body = isMorning
                    ? "Hãy ghi chép các chỉ số sức khỏe buổi sáng như: nhiệt độ, huyết áp, nhịp tim..."
                    : "Hãy ghi chép các chỉ số sức khỏe buổi tối như: cân nặng, đường huyết, giấc ngủ...";

                await notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patient.UserId,
                    Type = "healthlog_reminder",
                    Title = title,
                    Body = body,
                    Metadata = new Dictionary<string, object>
                    {
                        ["reminderType"] = reminderType,
                        ["date"] = DateOnly.FromDateTime(localNow).ToString("O")
                    }
                }, context.CancellationToken);

                sentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JOB-04] Failed to send reminder to user {UserId}", patient.UserId);
            }
        }

        _logger.LogInformation("[JOB-04] Health log reminder job completed. Sent {Count}/{Total} reminders",
            sentCount, patients.Count);
    }
}
