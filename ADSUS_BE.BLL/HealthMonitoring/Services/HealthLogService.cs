using ADSUS_BE.BLL.HealthMonitoring.DTOs;
using ADSUS_BE.BLL.HealthMonitoring.Interfaces;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace ADSUS_BE.BLL.HealthMonitoring.Services;

/// <summary>
/// Service implementation for Health Monitoring (UC-21, FT-35).
/// Based on API Spec Module09 endpoints #55 and #56.
/// </summary>
public class HealthLogService : IHealthLogService
{
    private readonly IHealthLogRepository _healthLogRepository;
    private readonly ILogger<HealthLogService> _logger;

    public HealthLogService(IHealthLogRepository healthLogRepository, ILogger<HealthLogService> logger)
    {
        _healthLogRepository = healthLogRepository;
        _logger = logger;
    }

    public async Task<HealthLogResponse> LogHealthDataAsync(
        LogHealthDataRequest request,
        Guid patientProfileId,
        CancellationToken cancellationToken = default)
    {
        var logType = Enum.Parse<HealthLogType>(request.Type!, ignoreCase: true);

        var healthLog = new HealthLog
        {
            HealthLogId = Guid.NewGuid(),
            PatientProfileId = patientProfileId,
            LogDate = DateOnly.FromDateTime(DateTime.UtcNow),
            LogType = logType,
            Content = request.Content!.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        var created = await _healthLogRepository.CreateAsync(healthLog, cancellationToken);

        _logger.LogInformation(
            "Health log created: {HealthLogId} for patient {PatientProfileId}",
            created.HealthLogId,
            patientProfileId);

        return MapToResponse(created);
    }

    public async Task<IReadOnlyList<HealthLogResponse>> GetHealthLogsAsync(
        Guid patientProfileId,
        HealthLogSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        var date = criteria.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var logs = await _healthLogRepository.GetByPatientAndDateAsync(
            patientProfileId,
            date,
            cancellationToken);

        return logs.Select(MapToResponse).ToList();
    }

    private static HealthLogResponse MapToResponse(HealthLog healthLog)
    {
        return new HealthLogResponse
        {
            HealthLogId = healthLog.HealthLogId,
            PatientProfileId = healthLog.PatientProfileId,
            LogDate = healthLog.LogDate,
            Type = healthLog.LogType.ToString(),
            Content = healthLog.Content,
            CreatedAt = healthLog.CreatedAt,
        };
    }
}
