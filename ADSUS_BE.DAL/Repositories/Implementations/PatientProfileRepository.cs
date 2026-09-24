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
            .Include(p => p.PatientDiseases).ThenInclude(x => x.Disease)
            .Include(p => p.PatientAllergies).ThenInclude(x => x.AllergyType)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.PatientProfileId == patientProfileId, ct);

    public Task<PatientProfile?> GetForUpdateAsync(Guid patientProfileId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .Include(p => p.User)
            .Include(p => p.PatientDiseases)
            .Include(p => p.PatientAllergies)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.PatientProfileId == patientProfileId, ct);

    public Task<Guid?> FindIdByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => (Guid?)p.PatientProfileId)
            .FirstOrDefaultAsync(ct);

    public Task<PatientProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.PatientDiseases).ThenInclude(x => x.Disease)
            .Include(p => p.PatientAllergies).ThenInclude(x => x.AllergyType)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public Task<bool> ExistsForUserAsync(Guid userId, CancellationToken ct = default) =>
        _db.PatientProfiles.AnyAsync(p => p.UserId == userId, ct);

    public Task<PatientProfile?> GetForUpdateByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        _db.PatientProfiles
            .Include(p => p.User)
            .Include(p => p.PatientDiseases)
            .Include(p => p.PatientAllergies)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task<PatientProfile?> FindGuestByPhoneForUpdateAsync(string phone, bool lockRow = false, CancellationToken ct = default)
    {
        if (lockRow && _db.Database.IsRelational())
        {
            return await _db.PatientProfiles
                .FromSqlRaw(
                    "SELECT * FROM patient_profiles WHERE phone = {0} AND user_id IS NULL FOR UPDATE",
                    phone)
                .FirstOrDefaultAsync(ct);
        }

        return await _db.PatientProfiles
            .FirstOrDefaultAsync(p => p.UserId == null && p.Phone == phone, ct);
    }

    public async Task StageAddAsync(PatientProfile profile, CancellationToken ct = default) =>
        await _db.PatientProfiles.AddAsync(profile, ct);

    public async Task<PatientProfile> StageForNewPatientAsync(User patient, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        var guestProfile = await FindGuestByPhoneForUpdateAsync(patient.Phone, lockRow: false, ct);

        if (guestProfile != null)
        {
            // Cùng cách PatientSelfRegistrationService nhận lại guest profile: các trường guest
            // chỉ dùng khi user_id IS NULL, từ giờ họ tên/sđt/ngày sinh lấy từ bảng users.
            guestProfile.UserId = patient.UserId;
            guestProfile.FullName = null;
            guestProfile.Phone = null;
            guestProfile.DateOfBirth = null;
            guestProfile.UpdatedAt = now;
            return guestProfile;
        }

        var profile = new PatientProfile
        {
            PatientProfileId = Guid.NewGuid(),
            UserId = patient.UserId,
            // Đứng tên chính bệnh nhân, không phải Admin/Điều dưỡng đang thao tác — đây chưa
            // phải hồ sơ nền (xem PatientProfileBaseline).
            CreatedBy = patient.UserId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        _db.PatientProfiles.Add(profile);
        return profile;
    }

    public async Task<Guid> EnsureForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var existingId = await FindIdByUserIdAsync(userId, ct);
        if (existingId.HasValue) return existingId.Value;

        var patient = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new InvalidOperationException("Patient account not found.");

        var staged = await StageForNewPatientAsync(patient, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
            return staged.PatientProfileId;
        }
        catch (DbUpdateException)
        {
            // Hai request cùng lúc cùng tạo bù — uq_patient_profiles_user chặn request thứ hai.
            // Bỏ bản ghi vừa gắn khỏi context rồi dùng bản của request kia.
            _db.Entry(staged).State = EntityState.Detached;
            return await FindIdByUserIdAsync(userId, ct) ?? throw new InvalidOperationException("Patient profile not found.");
        }
    }

    public async Task<PatientProfile> AddAsync(PatientProfile profile, CancellationToken ct = default)
    {
        _db.PatientProfiles.Add(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task ClearCollectionsAsync(Guid patientProfileId, CancellationToken ct = default)
    {
        await _db.PatientDiseases.Where(d => d.PatientProfileId == patientProfileId).ExecuteDeleteAsync(ct);
        await _db.PatientAllergies.Where(a => a.PatientProfileId == patientProfileId).ExecuteDeleteAsync(ct);
    }

    public async Task UpdateAsync(PatientProfile profile, CancellationToken ct = default)
    {
        // 1. Lấy danh sách ID hiện có trong context mà đã bị đánh dấu xóa (Deleted)
        // để tránh EF Core gửi DELETE lệnh có thể gây ra DbUpdateConcurrencyException
        var deletedDiseases = _db.ChangeTracker.Entries<PatientDisease>()
            .Where(e => e.State == EntityState.Deleted).Select(e => e.Entity).ToList();
        
        var deletedAllergies = _db.ChangeTracker.Entries<PatientAllergy>()
            .Where(e => e.State == EntityState.Deleted).Select(e => e.Entity).ToList();

        // 2. Tách chúng ra khỏi ChangeTracker
        foreach(var d in deletedDiseases) _db.Entry(d).State = EntityState.Detached;
        foreach(var a in deletedAllergies) _db.Entry(a).State = EntityState.Detached;

        // 3. Xóa trực tiếp bằng ExecuteDeleteAsync để đảm bảo an toàn, không sợ concurrency
        await ClearCollectionsAsync(profile.PatientProfileId, ct);

        // 4. Các item mới được thêm vào profile (State = Added) sẽ được EF xử lý bằng INSERT bình thường.
        if (_db.Entry(profile).State == EntityState.Detached)
        {
            _db.PatientProfiles.Update(profile);
        }

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
                        select new
                        {
                            User = u,
                            Profile = p,
                            // Điều kiện phải khớp PatientProfileBaseline.IsEstablishedBy — viết
                            // tay ở đây vì EF Core không dịch được lời gọi hàm C# sang SQL.
                            HasBaseline = p != null && _db.Users.Any(c => c.UserId == p.CreatedBy
                                && (c.Role == UserRole.Doctor || c.Role == UserRole.Staff)),
                        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = $"%{search.Trim()}%";

            // ILike là so khớp không phân biệt hoa thường đúng chuẩn Postgres (UC-09 BR-01).
            baseQuery = baseQuery.Where(x =>
                EF.Functions.ILike(EF.Functions.Unaccent(x.User.FullName), EF.Functions.Unaccent(keyword))
                || EF.Functions.ILike(x.User.Phone, keyword));
        }

        if (hasProfile == true)
        {
            baseQuery = baseQuery.Where(x => x.HasBaseline);
        }
        else if (hasProfile == false)
        {
            baseQuery = baseQuery.Where(x => !x.HasBaseline);
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
                x.HasBaseline,
                LatestCase = x.Profile != null && latestCaseByProfile.TryGetValue(x.Profile.PatientProfileId, out var lc)
                    ? lc
                    : null,
            })
            .AsEnumerable();

        if (string.Equals(visitStatus, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            merged = merged.Where(x => x.LatestCase != null
                && (x.LatestCase.Status == CaseStatus.Booked
                 || x.LatestCase.Status == CaseStatus.InProgress));
        }
        else if (string.Equals(visitStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            merged = merged.Where(x => x.LatestCase != null
                && (x.LatestCase.Status == CaseStatus.Confirmed
                 || x.LatestCase.Status == CaseStatus.End));
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
                // ToApiString(CaseStatus) hiện tại (xem chú thích tại EnumExtensions.cs).
                LatestVisitStatus: x.LatestCase == null ? null : (x.LatestCase.Status == CaseStatus.InProgress ? "IN_PROGRESS" : x.LatestCase.Status.ToString().ToUpperInvariant()),
                LatestCaseId: x.LatestCase?.CaseId,
                HasBaselineProfile: x.HasBaseline))
            .ToList();

        return (rows, total);
    }
}
