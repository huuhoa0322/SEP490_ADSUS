using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IPatientProfileRepository
{
    /// <summary>Đọc để hiển thị. Có Include(User) vì response cần họ tên/sđt/ngày sinh.</summary>
    Task<PatientProfile?> GetByIdAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>Đọc để sửa — có tracking.</summary>
    Task<PatientProfile?> GetForUpdateAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>Tìm hồ sơ theo tài khoản người dùng (dùng khi bệnh nhân tự xem hồ sơ mình).</summary>
    Task<PatientProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Chặn tạo hồ sơ thứ hai cho cùng một tài khoản (uq_patient_profiles_user).</summary>
    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken ct = default);

    Task<PatientProfile> AddAsync(PatientProfile profile, CancellationToken ct = default);

    Task UpdateAsync(PatientProfile profile, CancellationToken ct = default);

    /// <summary>
    /// UC-09 — danh sách bệnh nhân kèm ca khám gần nhất, sắp theo lần khám mới nhất.
    ///
    /// Truy vấn xuất phát từ bảng users (role = PATIENT) rồi LEFT JOIN sang patient_profiles,
    /// KHÔNG xuất phát từ patient_profiles như bản trước — nếu không thì tài khoản chưa có hồ
    /// sơ nền không bao giờ xuất hiện, và luồng tạo hồ sơ nền (#17) không có cách nào lấy được
    /// patientUserId.
    /// </summary>
    /// <param name="visitStatus">null = tất cả; "Pending" = ca mới nhất ở CREATED/ANALYZED; "Confirmed" = CONFIRMED.</param>
    /// <param name="hasProfile">null = tất cả; true = chỉ người đã có hồ sơ nền; false = chỉ người chưa có.</param>
    Task<(IReadOnlyList<PatientListRow> Items, int TotalCount)> SearchAsync(
        string? search,
        string? visitStatus,
        bool? hasProfile,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
