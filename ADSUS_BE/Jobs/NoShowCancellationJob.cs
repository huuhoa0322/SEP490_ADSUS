using ADSUS_BE.BLL.AppointmentScheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
/// - Từ 8:15 mà chưa checkin → Auto-NoShow
///
/// Toàn bộ logic nằm ở <see cref="NoShowService.ProcessOverdueAsync"/> — dùng chung với luồng
/// check-in muộn (P11 review 25/09/2026: trước đây job có bản logic riêng lệch với service và
/// truy vấn thẳng AppDbContext).
/// </summary>
[DisallowConcurrentExecution]
public sealed class NoShowCancellationJob : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NoShowCancellationJob> _logger;

    public NoShowCancellationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<NoShowCancellationJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _scopeFactory.CreateScope();
        var noShowService = scope.ServiceProvider.GetRequiredService<NoShowService>();

        _logger.LogInformation("[JOB-08] No-show cancellation job started at {Time}", DateTime.UtcNow);

        var noShowCount = await noShowService.ProcessOverdueAsync(context.CancellationToken);

        _logger.LogInformation("[JOB-08] No-show cancellation job completed. No-show: {NoShow}", noShowCount);
    }
}
