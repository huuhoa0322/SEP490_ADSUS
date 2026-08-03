using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.Common;

/// <summary>
/// Converts enums to the string form used in API responses and JWT claims.
/// C# members are PascalCase (Admin) while the database labels and the value clients expect
/// are uppercase (ADMIN). Keeping the conversion in one place means a change of convention
/// only has to be made here.
/// </summary>
public static class EnumExtensions
{
    public static string ToApiString(this UserRole role) => role.ToString().ToUpperInvariant();

    public static string ToApiString(this UserStatus status) => status.ToString().ToUpperInvariant();

    /// <summary>
    /// Đọc vai trò từ chuỗi client gửi lên. Trả null nếu không khớp giá trị nào.
    ///
    /// Phải tự đọc thay vì để bộ chuyển đổi JSON làm: mặc định nó biến chuỗi lạ thành phần
    /// tử ĐẦU TIÊN của enum, mà phần tử đầu tiên ở đây là Admin — gõ sai một chữ là vô tình
    /// tạo ra tài khoản quản trị.
    /// </summary>
    public static UserRole? ParseUserRole(string? value) =>
        Enum.TryParse<UserRole>(value, ignoreCase: true, out var role) && Enum.IsDefined(role)
            ? role
            : null;

    public static UserStatus? ParseUserStatus(string? value) =>
        Enum.TryParse<UserStatus>(value, ignoreCase: true, out var status) && Enum.IsDefined(status)
            ? status
            : null;

    /// <summary>
    /// Đọc giới tính từ chuỗi client gửi lên. Trả null nếu không khớp giá trị nào.
    /// Tự đọc thay vì để bộ chuyển đổi JSON làm — cùng lý do như ParseUserRole ở trên.
    /// </summary>
    public static GenderType? ParseGenderType(string? value) =>
        Enum.TryParse<GenderType>(value, ignoreCase: true, out var gender) && Enum.IsDefined(gender)
            ? gender
            : null;
}
