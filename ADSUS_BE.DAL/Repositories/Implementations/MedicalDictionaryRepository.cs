using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public sealed class MedicalDictionaryRepository : IMedicalDictionaryRepository
{
    private readonly AppDbContext _db;

    public MedicalDictionaryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MedicalDisease>> ListDiseasesAsync(CancellationToken ct = default)
    {
        return await _db.MedicalDiseases
            .AsNoTracking()
            .OrderBy(x => x.IsOther)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MedicalAllergyType>> ListAllergyTypesAsync(CancellationToken ct = default)
    {
        return await _db.MedicalAllergyTypes
            .AsNoTracking()
            .OrderBy(x => x.IsOther)
            .ThenBy(x => x.Name)
            .ToListAsync(ct);
    }
}
