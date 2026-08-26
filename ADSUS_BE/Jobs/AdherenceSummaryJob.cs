using ADSUS_BE.BLL.Common.Interfaces;
using ADSUS_BE.BLL.PrescriptionAdherence.Interfaces;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

/// <summary>
/// JOB-06 — Gửi tổng kết tuân thủ uống thuốc hàng ngày.
/// Chạy lúc 23h mỗi ngày.
/// GB-08: chỉ gửi push notification, không gửi Email/SMS.
/// </summary>
[DisallowConcurrentExecution]
public sealed class AdherenceSummaryJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<AdherenceSummaryJob> _logger;

    public AdherenceSummaryJob(
        IServiceScopeFactory scopeFactory,
        IUserRepository userRepo,
        ILogger<AdherenceSummaryJob> logger)
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
        _logger.LogInformation("[JOB-06] Adherence summary job started at {Time}", now);

        var today = DateOnly.FromDateTime(now);

        // Gửi notification cho tất cả bệnh nhân có đơn thuốc ACTIVE
        var patients = await _userRepo.GetAllPatientsAsync(context.CancellationToken);
        _logger.LogInformation("[JOB-06] Processing {Count} patients for daily adherence summary", patients.Count);

        var sentCount = 0;

        foreach (var patient in patients)
        {
            try
            {
                // TODO: Tính toán adherence rate từ medication intake logs trong ngày
                // Hiện tại gửi notification đơn giản, sau này có thể mở rộng với stats thực tế
                var hcmTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
                var localDate = TimeZoneInfo.ConvertTimeFromUtc(now, hcmTimeZone).Date;

                var body = $"Hãy kiểm tra lại việc uống thuốc hôm nay ({localDate:dd/MM/yyyy}) và đảm bảo tuân thủ đúng lịch trình.";

                await notificationService.SendAsync(new SendNotificationRequest
                {
                    UserId = patient.UserId,
                    Type = "adherence_summary",
                    Title = "Tổng kết uống thuốc hàng ngày",
                    Body = body,
                    Metadata = new Dictionary<string, object>
                    {
                        ["date"] = today.ToString("O"),
                        ["localDate"] = localDate.ToString("O")
                    }
                }, context.CancellationToken);

                sentCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[JOB-06] Failed to send summary to user {UserId}", patient.UserId);
            }
        }

        _logger.LogInformation("[JOB-06] Adherence summary job completed. Sent {Count}/{Total} summaries",
            sentCount, patients.Count);
    }
}
