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

    /// <summary>PatientProfileId của tài khoản, null nếu tài khoản chưa có hồ sơ. Chỉ đọc 1 cột.</summary>
    Task<Guid?> FindIdByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Chặn tạo hồ sơ thứ hai cho cùng một tài khoản (uq_patient_profiles_user).</summary>
    Task<bool> ExistsForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đọc để sửa theo tài khoản — có tracking (UC-06 lập hồ sơ nền trên bản ghi tạo sẵn).</summary>
    Task<PatientProfile?> GetForUpdateByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gắn sẵn hồ sơ cho tài khoản PATIENT vừa tạo, CHƯA lưu — bên gọi SaveChanges cùng lượt
    /// với chính tài khoản. Nếu số điện thoại trùng một guest profile (người thân từng được
    /// đặt hộ) thì nhận lại hồ sơ đó thay vì tạo mới, giống luồng tự đăng ký.
    /// </summary>
    Task<PatientProfile> StageForNewPatientAsync(User patient, CancellationToken ct = default);

    /// <summary>
    /// Guest profile (người thân chưa có tài khoản, user_id IS NULL) có số điện thoại này — CÓ
    /// tracking. <paramref name="lockRow"/> = true khoá dòng (SELECT ... FOR UPDATE) trong
    /// transaction đang mở, chống hai request đăng ký cùng số cùng nhận một hồ sơ; gọi hàm này
    /// trước <see cref="StageForNewPatientAsync"/> thì hàm đó nhận lại đúng dòng đã khoá (EF trả
    /// instance đang track). Chỉ có tác dụng với DB quan hệ.
    /// </summary>
    Task<PatientProfile?> FindGuestByPhoneForUpdateAsync(string phone, bool lockRow = false, CancellationToken ct = default);

    /// <summary>Thêm hồ sơ vào context, CHƯA lưu — khác <see cref="AddAsync"/> (lưu ngay).</summary>
    Task StageAddAsync(PatientProfile profile, CancellationToken ct = default);

    /// <summary>
    /// Trả về PatientProfileId của tài khoản, tạo bù nếu chưa có (tài khoản Admin/Điều dưỡng
    /// tạo trước 24/09/2026 không được gắn hồ sơ). Dùng ở các thao tác bệnh nhân tự làm như
    /// đặt lịch, ghi nhật ký sức khỏe.
    /// </summary>
    Task<Guid> EnsureForUserAsync(Guid userId, CancellationToken ct = default);

    Task<PatientProfile> AddAsync(PatientProfile profile, CancellationToken ct = default);

    Task ClearCollectionsAsync(Guid patientProfileId, CancellationToken ct = default);

    Task UpdateAsync(PatientProfile profile, CancellationToken ct = default);

    /// <summary>
    /// UC-09 — danh sách bệnh nhân kèm ca khám gần nhất, sắp theo lần khám mới nhất.
    ///
    /// Truy vấn xuất phát từ bảng users (role = PATIENT) rồi LEFT JOIN sang patient_profiles,
    /// KHÔNG xuất phát từ patient_profiles như bản trước — nếu không thì tài khoản chưa có hồ
    /// sơ nền không bao giờ xuất hiện, và luồng tạo hồ sơ nền (#17) không có cách nào lấy được
    /// patientUserId.
    /// </summary>
    /// <param name="visitStatus">null = tất cả; "Pending" = ca mới nhất ở InProgress/End; "Confirmed" = Confirmed.</param>
    /// <param name="hasProfile">null = tất cả; true = chỉ người đã lập hồ sơ nền; false = chỉ người chưa lập (xem <see cref="PatientProfileBaseline"/>).</param>
    Task<(IReadOnlyList<PatientListRow> Items, int TotalCount)> SearchAsync(
        string? search,
        string? visitStatus,
        bool? hasProfile,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
