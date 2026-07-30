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
    /// UC-04 AF-04 để ngỏ trường hợp này. Ở đây chặn tự khoá/tự vô hiệu hoá chính mình, vì
    /// làm được thì Admin tự nhốt mình ra ngoài hệ thống và không còn ai mở ra được.
    /// </summary>
    CannotTargetSelf,

    /// <summary>
    /// BR-05 — tài khoản đã bị vô hiệu hoá thì không quay lại được nữa.
    /// Deactivated là trạng thái cuối, PRD không định nghĩa đường kích hoạt lại.
    /// </summary>
    AccountIsDeactivated,
}
