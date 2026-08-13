using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// UC-04 — Admin quản lý tài khoản đăng nhập (FT-07 tạo, FT-08 khoá/vô hiệu hoá, FT-09 phân quyền).
///
/// Chỉ đụng tới TÀI KHOẢN ĐĂNG NHẬP, không chạm dữ liệu y tế.
/// </summary>
public interface IUserAccountService
{
    /// <summary>
    /// SCR-06 — danh sách tài khoản, tìm theo tên/số điện thoại và lọc theo vai trò, trạng thái.
    /// </summary>
    /// <param name="actingAdminId">
    /// Admin đang xem. Dùng để đánh dấu dòng của chính họ, cho giao diện ẩn nút khoá và vô
    /// hiệu hoá ở dòng đó.
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
    /// FT-07 — tạo tài khoản mới. Sinh mật khẩu tạm, đặt cờ buộc đổi mật khẩu, gửi email.
    /// Mật khẩu tạm KHÔNG nằm trong giá trị trả về (PRD §6.2).
    /// </summary>
    /// <param name="actingAdminId">Admin đang thao tác, để ghi nhật ký.</param>
    Task<(AccountOperationResult Result, UserAccountResponse? Account)> CreateAsync(
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
    /// FT-08 AF-01 — khoá hoặc mở khoá, hoàn toàn thủ công (BR-04).
    /// Không có job tự mở khoá; đây là đường duy nhất đi từ Locked về Active.
    /// </summary>
    /// <param name="actingAdminId">
    /// Id của Admin đang thao tác. Dùng để chặn tự khoá chính mình — Admin khoá Admin KHÁC
    /// thì được (quyết định của nhóm 31/07/2026 cho UC-04 AF-04).
    /// </param>
    Task<AccountOperationResult> SetLockedAsync(
        Guid userId,
        bool locked,
        Guid actingAdminId,
        CancellationToken cancellationToken = default);
}
