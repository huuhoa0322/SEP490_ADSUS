namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>Tự đăng ký — bước duy nhất còn lại sau khi đổi sang Firebase Phone Auth. Số điện
/// thoại KHÔNG nằm trong DTO này — lấy từ Firebase ID Token đã verify (xem Global Constraints
/// trong plan gốc: không bao giờ tin số điện thoại client tự gửi).</summary>
public record CompleteRegistrationRequest(
    string FirebaseIdToken,
    string FullName,
    string Password,
    string ConfirmPassword,
    string? Email,
    string? DateOfBirth);
