using ADSUS_BE.DAL.Data;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ADSUS_BE.DAL.Repositories.Implementations;

/// <summary>
/// EF Core implementation của IPatientProfileRepository (UC-06, UC-09).
/// KHÔNG có Remove: GB-03 cấm xoá dữ liệu y tế.
/// </summary>
public sealed class PatientProfileRepository : IPatientProfileRepository
{
    private readonly AppDbContext _db;

    public PatientProfileRepository(AppDbContext db) => _db = db;

    public Task<PatientProfile?> GetByIdAsync(Guid patientProfileId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientProfileId == patientProfileId, ct);

    public Task<PatientProfile?> GetForUpdateAsync(Guid patientProfileId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.PatientProfileId == patientProfileId, ct);

    public Task<PatientProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public Task<bool> ExistsForUserAsync(Guid userId, CancellationToken ct = default) =>
        _db.PatientProfiles.AnyAsync(p => p.UserId == userId, ct);

    public async Task<PatientProfile> AddAsync(PatientProfile profile, CancellationToken ct = default)
    {
        _db.PatientProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task UpdateAsync(PatientProfile profile, CancellationToken ct = default)
    {
        _db.PatientProfiles.Update(profile);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<(PatientProfile Profile, Case? LatestCase)> Items, int TotalCount)> SearchAsync(
        string? search,
        string? visitStatus,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Gắn ca khám mới nhất vào từng hồ sơ ngay trong một câu truy vấn. Lặp từng bệnh
        // nhân rồi hỏi ca mới nhất là N+1 — index idx_cases_patient_timeline đỡ sẵn câu này.
        var query = _db.PatientProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Select(p => new
            {
                Profile = p,
                LatestCase = p.Cases
                    .OrderByDescending(c => c.VisitDate)
                    .ThenByDescending(c => c.CreatedAt)
                    .FirstOrDefault(),
            });

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";

            // ILike là so khớp không phân biệt hoa thường đúng chuẩn Postgres (UC-09 BR-01),
            // dùng được index trigram nếu sau này thêm.
            query = query.Where(x =>
                EF.Functions.ILike(x.Profile.User.FullName, keyword)
                || EF.Functions.ILike(x.Profile.User.Phone, keyword));
        }

        if (string.Equals(visitStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.LatestCase != null
                && (x.LatestCase.Status == CaseStatus.Created || x.LatestCase.Status == CaseStatus.Analyzed));
        }
        else if (string.Equals(visitStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.LatestCase != null && x.LatestCase.Status == CaseStatus.Confirmed);
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            // Bệnh nhân chưa có ca nào xuống cuối, thay vì lên đầu như mặc định của null.
            .OrderByDescending(x => x.LatestCase != null ? x.LatestCase.VisitDate : DateOnly.MinValue)
            .ThenBy(x => x.Profile.User.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(x => (x.Profile, x.LatestCase))
            .ToList();

        return (items, total);
    }
}
