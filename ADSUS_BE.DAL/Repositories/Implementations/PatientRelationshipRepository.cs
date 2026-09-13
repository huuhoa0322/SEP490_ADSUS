using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IPatientRelationshipRepository.
/// </summary>
public sealed class PatientRelationshipRepository : IPatientRelationshipRepository
{
    private readonly AppDbContext _db;

    public PatientRelationshipRepository(AppDbContext db) => _db = db;

    public async Task<PatientRelationship?> GetByIdAsync(Guid relationshipId, CancellationToken ct = default)
    {
        return await _db.PatientRelationships
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RelationshipId == relationshipId, ct);
    }

    public async Task<PatientRelationship?> GetByIdWithPatientAsync(Guid relationshipId, CancellationToken ct = default)
    {
        return await _db.PatientRelationships
            .AsNoTracking()
            .Include(r => r.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.RelationshipId == relationshipId, ct);
    }

    public async Task<IReadOnlyList<PatientRelationship>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _db.PatientRelationships
            .AsNoTracking()
            .Include(r => r.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(r => r.User)
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.RelationshipName)
            .ToListAsync(ct);
    }

    public async Task<PatientRelationship> AddAsync(PatientRelationship relationship, CancellationToken ct = default)
    {
        relationship.CreatedAt = DateTime.UtcNow;
        _db.PatientRelationships.Add(relationship);
        await _db.SaveChangesAsync(ct);
        return relationship;
    }

    public async Task UpdateAsync(PatientRelationship relationship, CancellationToken ct = default)
    {
        _db.PatientRelationships.Update(relationship);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid relationshipId, CancellationToken ct = default)
    {
        var relationship = await _db.PatientRelationships
            .FirstOrDefaultAsync(r => r.RelationshipId == relationshipId, ct);

        if (relationship != null)
        {
            _db.PatientRelationships.Remove(relationship);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid patientProfileId, CancellationToken ct = default)
    {
        return await _db.PatientRelationships
            .AsNoTracking()
            .AnyAsync(r => r.UserId == userId && r.PatientProfileId == patientProfileId, ct);
    }

    public async Task<PatientRelationship?> GetByIdAndUserAsync(Guid relationshipId, Guid userId, CancellationToken ct = default)
    {
        return await _db.PatientRelationships
            .AsNoTracking()
            .Include(r => r.PatientProfile)
                .ThenInclude(p => p.User)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.RelationshipId == relationshipId && r.UserId == userId, ct);
    }

    public async Task<bool> IsPhoneRegisteredAsync(string phone, CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .AnyAsync(u => u.Phone == phone, ct);
    }
}
