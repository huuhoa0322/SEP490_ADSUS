using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

public sealed class SymptomCategoryRepository : ISymptomCategoryRepository
{
    private readonly AppDbContext _db;

    public SymptomCategoryRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SymptomCategory>> GetAllWithSymptomsAsync(CancellationToken ct = default)
    {
        return await _db.SymptomCategories
            .Include(c => c.Symptoms)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
