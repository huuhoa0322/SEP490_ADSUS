using System.Globalization;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.UserRoleManagement.Mappers;

/// <summary>
/// Thêm 12/08/2026 (P11 review Module 2) — trước đó ToResponse là private static method ngay
/// trong UserAccountService, không có Mapper riêng như L3 §6 quy định. Không có rủi ro rò rỉ
/// (mọi field đã liệt kê tường minh, có lọc DOB theo BR-01), chỉ là lệch cấu trúc thư mục —
/// tách ra đây để đúng convention, không đổi field nào.
/// </summary>
public static class UserAccountMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    public static UserAccountResponse ToResponse(User user, Guid actingAdminId) => new()
    {
        IsCurrentUser = user.UserId == actingAdminId,
        UserId = user.UserId,
        PhoneNumber = user.Phone,
        FullName = user.FullName,
        Email = user.Email,
        Role = user.Role.ToApiString(),
        Status = user.Status.ToApiString(),
        // BR-01 — không trả ngày sinh của bệnh nhân cho giao diện quản trị.
        DateOfBirth = user.Role == UserRole.Patient
            ? null
            : user.DateOfBirth?.ToString(DateFormat, CultureInfo.InvariantCulture),
        MustChangePassword = user.MustChangePassword,
        CreatedAt = user.CreatedAt,
    };
}
