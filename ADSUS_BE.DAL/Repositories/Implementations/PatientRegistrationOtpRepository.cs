using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public class PatientRegistrationOtpRepository : IPatientRegistrationOtpRepository
{
    private readonly AppDbContext _db;

    public PatientRegistrationOtpRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(PatientRegistrationOtp otp, CancellationToken cancellationToken = default)
    {
        await _db.PatientRegistrationOtps.AddAsync(otp, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PatientRegistrationOtp?> GetLatestByPhoneAsync(
        string phone, CancellationToken cancellationToken = default)
    {
        return await _db.PatientRegistrationOtps
            .Where(o => o.Phone == phone)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PatientRegistrationOtp?> GetByVerificationTokenHashAsync(
        string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _db.PatientRegistrationOtps
            .FirstOrDefaultAsync(o => o.VerificationTokenHash == tokenHash, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _db.SaveChangesAsync(cancellationToken);
}
