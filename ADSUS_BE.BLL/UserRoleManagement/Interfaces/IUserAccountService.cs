using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// UC-04 — Admin quản lý tài khoản đăng nhập (FT-07 tạo, FT-08 vô hiệu hoá, FT-09 phân quyền).
///
/// Chỉ đụng tới TÀI KHOẢN ĐĂNG NHẬP, không chạm dữ liệu y tế.
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// SCR-06 — danh sách tài khoản, tìm theo tên/số điện thoại và lọc theo vai trò, trạng thái.
    /// </summary>
    /// <param name="actingAdminId">
    /// Admin đang xem. Dùng để đánh dấu dòng của chính họ, cho giao diện ẩn nút vô hiệu hoá
    /// ở dòng đó.
    /// </param>
    Task<PagedResult<UserAccountResponse>> SearchAsync(
        string? keyword,
        string? role,
        string? status,
        int page,
        int pageSize,
        Guid actingAdminId,
        CancellationToken cancellationToken = default);

    /// <summary>SCR-07 — lấy một tài khoản để đổ vào form sửa.</summary>
    Task<UserAccountResponse?> GetByIdAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FT-07 — tạo tài khoản mới. Sinh mật khẩu tạm, đặt cờ buộc đổi mật khẩu, trả về plaintext
    /// MỘT LẦN để Admin đọc trực tiếp cho chủ tài khoản — không còn gửi qua email (sửa
    /// 12/08/2026, thống nhất với UC-03 AF-02/UC-06 AF-01/AF-03). UserAccountResponse
    /// (Account) không chứa mật khẩu dưới bất kỳ hình thức nào (PRD §6.2).
    /// </summary>
    /// <param name="actingAdminId">Admin đang thao tác, để ghi nhật ký.</param>
    Task<(AccountOperationResult Result, UserAccountResponse? Account, string? TemporaryPassword)> CreateAsync(
        CreateUserAccountRequest request,
        Guid actingAdminId,
        CancellationToken cancellationToken = default);

    /// <summary>FT-09 — sửa thông tin và phân lại vai trò.</summary>
    /// <param name="actingAdminId">Admin đang thao tác, để ghi nhật ký.</param>
    Task<AccountOperationResult> UpdateAsync(
        Guid userId,
        UpdateUserAccountRequest request,
        Guid actingAdminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// FT-08 — vô hiệu hoá tài khoản.
    /// Không bao giờ xoá cứng bản ghi; dữ liệu liên quan vẫn phải truy cập được.
    /// </summary>
        Task<AccountOperationResult> ReactivateAsync(
        Guid actingAdminId,
        Guid targetUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<AccountOperationResult> DeactivateAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default);
}
