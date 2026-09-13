namespace ADSUS_BE.BLL.UserRoleManagement.Interfaces;

/// <summary>
/// Xác minh Firebase ID Token (phát ra sau khi Mobile app xác thực số điện thoại thành công
/// qua Firebase Phone Auth SDK) và trả về số điện thoại ĐÃ ĐƯỢC GOOGLE CHỨNG THỰC, ở định
/// dạng nội địa (0xxxxxxxxx) — KHÔNG BAO GIỜ tin số điện thoại client tự gửi trong body request
/// (xem Global Constraints).
///
/// Hợp đồng: KHÔNG BAO GIỜ throw ra ngoài — token sai chữ ký, hết hạn, sai project, hay thiếu
/// claim phone_number đều trả về null, để tầng gọi tự quyết cách phản hồi.
/// </summary>
public interface IFirebasePhoneVerificationService
{
    Task<string?> VerifyAndGetLocalPhoneNumberAsync(
        string idToken, CancellationToken cancellationToken = default);
}
