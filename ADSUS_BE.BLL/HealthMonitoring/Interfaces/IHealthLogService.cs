using ADSUS_BE.BLL.HealthMonitoring.DTOs;

namespace ADSUS_BE.BLL.HealthMonitoring.Interfaces;

/// <summary>
/// Service interface for Health Monitoring (UC-21, FT-35).
/// </summary>
public interface IHealthLogService
{
    /// <summary>
    /// Logs a new health entry for a patient.
    /// </summary>
    Task<HealthLogResponse> LogHealthDataAsync(
        LogHealthDataRequest request,
        Guid patientProfileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets health logs for a patient on a specific date (defaults to today).
    /// </summary>
    Task<IReadOnlyList<HealthLogResponse>> GetHealthLogsAsync(
        Guid patientProfileId,
        HealthLogSearchCriteria criteria,
        CancellationToken cancellationToken = default);
}
