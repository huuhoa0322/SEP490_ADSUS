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
    /// Trạng thái ca khám — enum <c>case_status</c>. Không dùng .ToString().ToUpperInvariant():
    /// mọi nhãn ở đây là 1 từ nên hiện tại trùng kết quả, nhưng viết switch tường minh để nhất
    /// quán với ToApiString(AiResultStatus) bên dưới, nơi bắt buộc phải switch.
    /// </summary>
    public static string ToApiString(this CaseStatus status) => status switch
    {
        CaseStatus.Created => "CREATED",
        CaseStatus.End => "END",
        CaseStatus.Confirmed => "CONFIRMED",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    /// <summary>
    /// Giới tính hồ sơ y tế — enum <c>gender_type</c>.
    /// </summary>
    public static string ToApiString(this GenderType gender) => gender switch
    {
        GenderType.Female => "FEMALE",
        GenderType.Male => "MALE",
        GenderType.Other => "OTHER",
        _ => throw new ArgumentOutOfRangeException(nameof(gender)),
    };

    /// <summary>
    /// Trạng thái đơn thuốc — enum <c>prescription_status</c>.
    /// </summary>
    public static string ToApiString(this PrescriptionStatus status) => status switch
    {
        PrescriptionStatus.Active => "ACTIVE",
        PrescriptionStatus.Completed => "COMPLETED",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

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
