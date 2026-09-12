namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>Bước 3 (cuối) của tự đăng ký — tạo tài khoản Patient thật.</summary>
public record CompleteRegistrationRequest(
    string RegistrationToken,
    string FullName,
    string Password,
    string ConfirmPassword,
    string? Email,
    string? DateOfBirth);
