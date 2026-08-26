using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

/// <summary>
/// JOB-05 — Gửi báo cáo sức khỏe hàng tuần.
/// Chạy vào 9h sáng thứ 6 hàng tuần.
/// GB-08: chỉ gửi push notification, không gửi Email/SMS.
/// </summary>
[DisallowConcurrentExecution]
public sealed class WeeklyHealthReportJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<WeeklyHealthReportJob> _logger;

    public WeeklyHealthReportJob(
        IServiceScopeFactory scopeFactory,
        IUserRepository userRepo,
        ILogger<WeeklyHealthReportJob> logger)
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

        _logger.LogInformation("[JOB-05] Weekly health report job started at {Time}", DateTime.UtcNow);

        var now = DateTime.UtcNow;
        var weekStart = DateOnly.FromDateTime(now.AddDays(-7));
        var weekEnd = DateOnly.FromDateTime(now);

        // Gửi notification cho tất cả bệnh nhân
        var patients = await _userRepo.GetAllPatientsAsync(context.CancellationToken);
        _logger.LogInformation("[JOB-05] Processing {Count} patients for weekly report", patients.Count);

        var sentCount = 0;

        foreach (var patient in patients)
        {
            try
            {
                // TODO: Tính toán stats từ health logs trong tuần
                // Hiện tại gửi notification đơn giản, sau này có thể mở rộng với stats thực tế
                var weekNumber = GetIso8601WeekOfYear(now);
                var body = $"Tuần {weekNumber} ({weekStart:dd/MM} - {weekEnd:dd/MM}): Hãy kiểm tra lại các chỉ số sức khỏe của bạn trong tuần qua.";

                await notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patient.UserId,
                    Type = "weekly_health_report",
                    Title = $"Báo cáo sức khỏe tuần #{weekNumber}",
                    Body = body,
                    Metadata = new Dictionary<string, object>
                    {
                        ["weekNumber"] = weekNumber,
                        ["weekStart"] = weekStart.ToString("O"),
                        ["weekEnd"] = weekEnd.ToString("O")
                    }
                }, context.CancellationToken);

                sentCount++;
                _logger.LogInformation("[JOB-05] Sent weekly report to user {UserId}", patient.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JOB-05] Failed to send report to user {UserId}", patient.UserId);
            }
        }

        _logger.LogInformation("[JOB-05] Weekly health report job completed. Sent {Count}/{Total} reports",
            sentCount, patients.Count);
    }

    /// <summary>
    /// Lấy số tuần ISO 8601 trong năm.
    /// </summary>
    private static int GetIso8601WeekOfYear(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
        {
            date = date.AddDays(3);
        }
        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);
    }
}
