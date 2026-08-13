using System.Globalization;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.BLL.Auth.Mappers;

/// <summary>
/// Thêm 12/08/2026 (P11 review Module 1) — trước đó LoginResponse/UserProfileResponse được
/// dựng trực tiếp bằng object initializer ngay trong Service, không qua Mapper riêng như L3 §6
/// quy định cho các module khác. Không có rủi ro rò rỉ (mọi field đã liệt kê tường minh), chỉ
/// là lệch cấu trúc thư mục — tách ra đây để đúng convention, không đổi field nào.
/// </summary>
public static class UserMapper
{
    private const string DateFormat = "yyyy-MM-dd";

    public static LoginResponse ToLoginResponse(User user, string accessToken) => new()
    {
        AccessToken = accessToken,
        UserId = user.UserId,
        Role = user.Role.ToApiString(),
        FullName = user.FullName,
        Email = user.Email,
        MustChangePassword = user.MustChangePassword,
    };

    public static UserProfileResponse ToProfileResponse(User user) => new()
    {
        FullName = user.FullName,
        PhoneNumber = user.Phone,
        Email = user.Email,
        DateOfBirth = user.DateOfBirth?.ToString(DateFormat, CultureInfo.InvariantCulture),
        Role = user.Role.ToApiString(),
        BiometricEnabled = user.BiometricEnabled,
        MustChangePassword = user.MustChangePassword,
    };
}
