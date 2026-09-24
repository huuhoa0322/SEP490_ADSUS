using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.MedicalRecord.DTOs;

namespace ADSUS_BE.BLL.MedicalRecord.Interfaces;

public interface IPatientProfileService
{
    Task<PatientProfileResponse> CreateAsync(
        CreatePatientProfileRequest request, Guid actingUserId, CancellationToken ct = default);

    Task<PatientProfileResponse> UpdateAsync(
        Guid patientProfileId, UpdatePatientProfileRequest request, CancellationToken ct = default);

    Task<PatientProfileResponse> GetByIdAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>
    /// Như <see cref="GetByIdAsync"/> nhưng trả null thay vì ném lỗi khi không có hồ sơ — cho
    /// module khác cần thông tin bệnh nhân để gửi thông báo "nếu có" (vd AppointmentService).
    /// Hồ sơ guest (người thân chưa có tài khoản) trả PatientUserId = Guid.Empty.
    /// </summary>
    Task<PatientProfileResponse?> FindByIdAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>
    /// PatientProfileId của một tài khoản bệnh nhân, null nếu tài khoản chưa có hồ sơ — cho module
    /// khác chỉ cần biết "hồ sơ của người đang đăng nhập" (lời nhắc, lịch uống thuốc...). Không tạo bù.
    /// </summary>
    Task<Guid?> FindIdByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Hồ sơ (kèm bệnh nền, dị ứng) của một tài khoản bệnh nhân, null nếu tài khoản chưa có hồ sơ
    /// — cho module khác cần thông tin bệnh nhân đang đăng nhập (vd ngữ cảnh chatbot).
    /// </summary>
    Task<PatientProfileResponse?> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// UserId của nhiều hồ sơ trong MỘT truy vấn — cho module khác cần gửi thông báo hàng loạt.
    /// Khoá = PatientProfileId; giá trị null = guest profile (người thân chưa có tài khoản). Hồ sơ
    /// không tồn tại thì không có khoá.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, Guid?>> FindUserIdsAsync(
        IReadOnlyCollection<Guid> patientProfileIds, CancellationToken ct = default);

    // ─── Dành cho module khác tạo tài khoản / người thân (không tự lưu) ──────────────

    /// <summary>
    /// Id guest profile (người thân chưa có tài khoản) mang số điện thoại này, null nếu không có.
    /// <paramref name="lockForUpdate"/> = true khoá dòng đó tới hết transaction đang mở (đăng ký
    /// tài khoản — chống hai request cùng nhận một hồ sơ).
    /// </summary>
    Task<Guid?> FindGuestProfileIdByPhoneAsync(string phone, bool lockForUpdate, CancellationToken ct = default);

    /// <summary>
    /// Gắn hồ sơ cho tài khoản PATIENT vừa tạo (chưa lưu): nhận lại guest profile trùng số điện
    /// thoại (xoá các trường guest), không có thì tạo hồ sơ mới. Trả PatientProfileId. Bên gọi lưu
    /// cùng lượt với tài khoản.
    /// </summary>
    Task<Guid> StageForNewPatientAsync(ADSUS_BE.DAL.Entities.User patient, CancellationToken ct = default);

    /// <summary>Thông tin guest profile, null nếu không có hồ sơ hoặc hồ sơ đã gắn tài khoản.</summary>
    Task<GuestProfileInfo?> FindGuestProfileAsync(Guid patientProfileId, CancellationToken ct = default);

    /// <summary>
    /// Hồ sơ cho người thân được thêm vào danh bạ (chưa lưu): có số điện thoại thì nhận lại guest
    /// profile trùng số, không có thì tạo guest profile mới đứng tên <paramref name="createdBy"/>.
    /// </summary>
    Task<GuestProfileInfo> StageGuestProfileAsync(
        string fullName, string? phone, DateOnly? dateOfBirth, Guid createdBy, CancellationToken ct = default);

    /// <summary>
    /// Sửa thông tin guest profile (chưa lưu). Trường null = giữ nguyên; <paramref name="phone"/>
    /// rỗng = xoá số. Hồ sơ đã gắn tài khoản thì không đổi gì (thông tin lấy từ tài khoản).
    /// </summary>
    Task StageGuestProfileUpdateAsync(
        Guid patientProfileId, string? fullName, string? phone, DateOnly? dateOfBirth, CancellationToken ct = default);

    Task<PagedResult<PatientSummaryResponse>> SearchPatientsAsync(
        string? search,
        string? visitStatus,
        bool? hasProfile,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
