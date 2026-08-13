using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

/// <summary>
/// Repository interface for HealthLog entity.
/// Based on API Spec Module09, UC-21.
/// </summary>
public interface IHealthLogRepository
{
    /// <summary>
    /// Creates a new health log entry.
    /// </summary>
    Task<HealthLog> CreateAsync(HealthLog healthLog, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all health logs for a patient on a specific date, ordered by CreatedAt ASC.
    /// </summary>
    Task<IReadOnlyList<HealthLog>> GetByPatientAndDateAsync(
        Guid patientProfileId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recent health logs for a patient (for widget FT-41).
    /// </summary>
    Task<IReadOnlyList<HealthLog>> GetLatestByPatientAsync(
        Guid patientProfileId,
        int limit = 10,
        CancellationToken cancellationToken = default);
}
