namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>
/// Kết quả của một thao tác quản lý tài khoản.
///
/// Trả về enum thay vì ném ngoại lệ, để controller quyết định mã HTTP tương ứng — cùng cách
/// làm với ProfileOperationResult ở Module 1.
/// </summary>
public enum AccountOperationResult
{
    Success,

    /// <summary>Không tìm thấy tài khoản.</summary>
    NotFound,

    /// <summary>BR-02 — số điện thoại đã có tài khoản khác dùng.</summary>
    PhoneAlreadyUsed,

    /// <summary>Email đã có tài khoản khác dùng (DB có unique index trên lower(email)).</summary>
    EmailAlreadyUsed,

    /// <summary>Vai trò gửi lên không hợp lệ, hoặc là ADMIN — không tạo qua màn này được.</summary>
    InvalidRole,

    /// <summary>
    /// Admin đang thao tác lên chính tài khoản mình.
    ///
    /// UC-04 AF-04 để ngỏ trường hợp này; nhóm đã chốt ngày 31/07/2026:
    ///   - Admin ĐƯỢC khoá và vô hiệu hoá Admin khác.
    ///   - Admin KHÔNG được thao tác lên chính mình — làm được thì tự nhốt mình ra ngoài
    ///     hệ thống, và vì không có đường kích hoạt lại (BR-05) nên không ai cứu được.
    /// </summary>
    CannotTargetSelf,

    /// <summary>
    /// BR-05 — tài khoản đã bị vô hiệu hoá thì không quay lại được nữa.
    /// Deactivated là trạng thái cuối, PRD không định nghĩa đường kích hoạt lại.
    /// </summary>
    AccountIsDeactivated,

    /// <summary>
    /// UC-03 AF-02 — tài khoản chưa khai email nên không có chỗ nào để gửi mật khẩu tạm tới.
    ///
    /// Chỉ dùng cho đường Admin cấp lại hộ. Ở đường tự phục vụ thì không bao giờ trả về lỗi
    /// nào cả (AF-01).
    /// </summary>
    AccountHasNoEmail,
}
