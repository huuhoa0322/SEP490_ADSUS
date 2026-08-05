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

    public async Task<(IReadOnlyList<PatientListRow> Items, int TotalCount)> SearchAsync(
        string? search,
        string? visitStatus,
        bool? hasProfile,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Hai lần thử dịch "ca khám mới nhất của mỗi hồ sơ" thành MỘT câu SQL duy nhất đều gãy
        // trên Postgres thật, dù build sạch và test (mock) đều pass cả hai lần — EF Core 8 không
        // dịch được kiểu tương quan-subquery-tái-dùng-trong-OrderBy (lần 1), rồi không dịch được
        // GroupBy-subquery-làm-nguồn-JOIN kết hợp CountAsync (lần 2).
        //
        // Tách hẳn thành 2 câu SQL đơn giản (không GroupBy, không subquery làm nguồn JOIN) rồi
        // ghép/lọc/sắp bằng LINQ-to-Objects. Đánh đổi: tải nhiều hơn 1 trang mỗi lần — chấp nhận
        // được ở quy mô một phòng khám (UC-09 mặc định pageSize 20, không phải hệ thống hàng
        // triệu bản ghi). Đổi lại loại bỏ hoàn toàn rủi ro dịch SQL.

        // Bước 1 — chỉ LEFT JOIN + WHERE, đã xác nhận dịch được (câu COUNT đầu tiên từng chạy
        // đúng trước khi Case tham gia vào truy vấn).
        var baseQuery = from u in _db.Users.AsNoTracking()
                        where u.Role == UserRole.Patient
                        join p in _db.PatientProfiles.AsNoTracking() on u.UserId equals p.UserId into profiles
                        from p in profiles.DefaultIfEmpty()
                        select new { User = u, Profile = p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";

            // ILike là so khớp không phân biệt hoa thường đúng chuẩn Postgres (UC-09 BR-01).
            baseQuery = baseQuery.Where(x =>
                EF.Functions.ILike(x.User.FullName, keyword)
                || EF.Functions.ILike(x.User.Phone, keyword));
        }

        if (hasProfile == true)
        {
            baseQuery = baseQuery.Where(x => x.Profile != null);
        }
        else if (hasProfile == false)
        {
            baseQuery = baseQuery.Where(x => x.Profile == null);
        }

        var candidates = await baseQuery.ToListAsync(ct);

        // Bước 2 — WHERE ... IN (...) đơn giản, không GroupBy, không subquery làm nguồn JOIN.
        var profileIds = candidates
            .Where(x => x.Profile != null)
            .Select(x => x.Profile!.PatientProfileId)
            .ToList();

        var casesByProfile = profileIds.Count == 0
            ? new List<Case>()
            : await _db.Cases
                .AsNoTracking()
                .Where(c => profileIds.Contains(c.PatientProfileId))
                .ToListAsync(ct);

        // Từ đây trở đi là LINQ-to-Objects thuần — không còn gì để EF Core dịch, nên không còn
        // rủi ro translation nữa.
        var latestCaseByProfile = casesByProfile
            .GroupBy(c => c.PatientProfileId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(c => c.VisitDate).ThenByDescending(c => c.CreatedAt).First());

        var merged = candidates
            .Select(x => new
            {
                x.User,
                x.Profile,
                LatestCase = x.Profile != null && latestCaseByProfile.TryGetValue(x.Profile.PatientProfileId, out var lc)
                    ? lc
                    : null,
            })
            .AsEnumerable();

        if (string.Equals(visitStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            merged = merged.Where(x => x.LatestCase != null
                && (x.LatestCase.Status == CaseStatus.Created || x.LatestCase.Status == CaseStatus.Analyzed));
        }
        else if (string.Equals(visitStatus, "Confirmed", StringComparison.OrdinalIgnoreCase))
        {
            merged = merged.Where(x => x.LatestCase != null && x.LatestCase.Status == CaseStatus.Confirmed);
        }

        var mergedList = merged.ToList();
        var total = mergedList.Count;

        // .NET's default comparer for nullable value types treats null as smaller than any
        // non-null value, so OrderByDescending puts null LatestCase?.VisitDate rows last —
        // bệnh nhân chưa có ca nào xuống cuối, không cần giá trị sentinel nào.
        // ThenBy dùng StringComparer.Ordinal thay vì mặc định: đây là sắp trong bộ nhớ, không
        // cần khớp collation với Postgres (ILike ở Bước 1 chỉ lọc, không sắp) — Ordinal cho kết
        // quả tất định, không phụ thuộc culture của máy chạy, phù hợp vì đây chỉ là tiêu chí
        // phụ (tiêu chí chính là ngày khám).
        var rows = mergedList
            .OrderByDescending(x => x.LatestCase?.VisitDate)
            .ThenBy(x => x.User.FullName, StringComparer.Ordinal)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PatientListRow(
                PatientProfileId: x.Profile?.PatientProfileId,
                PatientUserId: x.User.UserId,
                FullName: x.User.FullName,
                Phone: x.User.Phone,
                LatestVisitDate: x.LatestCase?.VisitDate,
                // Không gọi CaseStatus.ToApiString() (ADSUS_BE.BLL.Common): DAL không — và không
                // nên — tham chiếu BLL (chiều phụ thuộc đúng là BLL -> DAL). Ba nhãn case_status
                // đều một từ nên .ToString().ToUpperInvariant() cho kết quả giống hệt
                // ToApiString(CaseStatus) hiện tại (xem chú thích tại EnumExtensions.cs).
                LatestVisitStatus: x.LatestCase?.Status.ToString().ToUpperInvariant()))
            .ToList();

        return (rows, total);
    }
}
