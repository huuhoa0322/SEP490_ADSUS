using System;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Mã OTP xác thực số điện thoại khi bệnh nhân tự đăng ký (Mobile app). Không liên quan tới
/// đăng nhập — <see cref="User.PasswordHash"/> vẫn là cơ chế đăng nhập duy nhất, không đổi.
///
/// Lưu ý: tên bảng/entity "Registration" (tuy chỉ dùng cho tự đăng ký ở phase này) là CÓ CHỦ ĐÍCH
/// giữ nguyên để tái sử dụng cho luồng quên-mật-khẩu-qua-SMS-OTP trong task sau — không đổi tên
/// là quyết định ghi trong plan, tránh thêm bảng riêng.
/// </summary>
public partial class PatientRegistrationOtp
{
    public Guid OtpId { get; set; }

    public string Phone { get; set; } = null!;

    /// <summary>SHA-256 hash của mã 6 số — không bao giờ lưu plaintext.</summary>
    public string OtpHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Số lần thử sai mã. Đạt ngưỡng thì hàng này bị vô hiệu — phải xin mã mới.</summary>
    public int AttemptCount { get; set; }

    /// <summary>NULL nếu chưa verify-otp thành công.</summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>SHA-256 hash của registration token — chỉ có giá trị sau khi verify-otp.</summary>
    public string? VerificationTokenHash { get; set; }

    public DateTime? VerificationTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
