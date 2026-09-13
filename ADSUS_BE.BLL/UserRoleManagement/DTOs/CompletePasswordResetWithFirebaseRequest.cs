namespace ADSUS_BE.BLL.UserRoleManagement.DTOs;

/// <summary>Quên mật khẩu qua Firebase — bước duy nhất còn lại. Số điện thoại KHÔNG nằm
/// trong DTO này — lấy từ Firebase ID Token đã verify.</summary>
public record CompletePasswordResetWithFirebaseRequest(
    string FirebaseIdToken,
    string NewPassword,
    string ConfirmNewPassword);
