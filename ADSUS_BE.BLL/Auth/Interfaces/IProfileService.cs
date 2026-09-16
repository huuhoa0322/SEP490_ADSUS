using ADSUS_BE.BLL.Auth.DTOs;

namespace ADSUS_BE.BLL.Auth.Interfaces;

public interface IProfileService
{
    /// <summary>
    /// UC-10 bước 2 — lấy hồ sơ hiện tại để hiển thị lên SCR-03.
    /// Trả về null nếu tài khoản không còn tồn tại.
    /// </summary>
    Task<UserProfileResponse?> GetOwnProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// UC-10 — cập nhật hồ sơ cá nhân. Chỉ sửa họ tên, email, ngày sinh.
    /// Số điện thoại và mọi dữ liệu y tế không bao giờ bị thay đổi ở đây (BR-02, BR-03).
    /// </summary>
    Task<ProfileOperationResult> UpdateOwnProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);
}
