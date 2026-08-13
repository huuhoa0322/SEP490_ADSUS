using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation of IHealthLogRepository.
/// Based on API Spec Module09, UC-21.
/// </summary>
public class HealthLogRepository : IHealthLogRepository
{
    private readonly AppDbContext _dbContext;

    public HealthLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthLog> CreateAsync(HealthLog healthLog, CancellationToken cancellationToken = default)
    {
        await _dbContext.HealthLogs.AddAsync(healthLog, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return healthLog;
    }

    public async Task<IReadOnlyList<HealthLog>> GetByPatientAndDateAsync(
        Guid patientProfileId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.HealthLogs
            .Where(h => h.PatientProfileId == patientProfileId && h.LogDate == date)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HealthLog>> GetLatestByPatientAsync(
        Guid patientProfileId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.HealthLogs
            .Where(h => h.PatientProfileId == patientProfileId)
            .OrderByDescending(h => h.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
