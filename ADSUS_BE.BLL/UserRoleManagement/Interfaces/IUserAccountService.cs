using ADSUS_BE.BLL.UserRoleManagement.DTOs;

namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// UC-04 — Admin quản lý tài khoản đăng nhập (FT-07 tạo, FT-08 khoá/vô hiệu hoá, FT-09 phân quyền).
///
/// Chỉ đụng tới TÀI KHOẢN ĐĂNG NHẬP, không chạm dữ liệu y tế.
/// </summary>
public interface IUserAccountService
{
    /// <summary>SCR-06 — danh sách tài khoản, có tìm kiếm theo tên/số điện thoại và lọc theo vai trò, trạng thái.</summary>
    Task<PagedResult<UserAccountResponse>> SearchAsync(
        string? keyword,
        string? role,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>SCR-07 — lấy một tài khoản để đổ vào form sửa.</summary>
    Task<UserAccountResponse?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// FT-07 — tạo tài khoản mới. Sinh mật khẩu tạm, đặt cờ buộc đổi mật khẩu, gửi email.
    /// Mật khẩu tạm KHÔNG nằm trong giá trị trả về (PRD §6.2).
    /// </summary>
    Task<(AccountOperationResult Result, UserAccountResponse? Account)> CreateAsync(
        CreateUserAccountRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>FT-09 — sửa thông tin và phân lại vai trò.</summary>
    Task<AccountOperationResult> UpdateAsync(
        Guid userId,
        UpdateUserAccountRequest request,
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

    /// <summary>
    /// FT-08 AF-02 — vô hiệu hoá vĩnh viễn. Một chiều, không có đường quay lại (BR-05).
    /// Không bao giờ xoá cứng bản ghi; dữ liệu liên quan vẫn phải truy cập được.
    /// </summary>
    Task<AccountOperationResult> DeactivateAsync(
        Guid userId,
        Guid actingAdminId,
        CancellationToken cancellationToken = default);
}
