using System;
using System.Collections.Generic;

namespace ADSUS_BE.DAL.Entities;

/// <summary>
/// Mã OTP xác thực số điện thoại khi bệnh nhân tự đăng ký (không qua Admin/Điều dưỡng). Không liên quan tới đăng nhập — users.password_hash vẫn là cơ chế đăng nhập duy nhất.
/// </summary>
public partial class PatientRegistrationOtp
{
    public Guid OtpId { get; set; }

    public string Phone { get; set; } = null!;

    /// <summary>
    /// SHA-256 hash của mã 6 số, không lưu plaintext. Không salt — mã có hiệu lực 5 phút và tối đa 5 lần thử sai, đủ giảm rủi ro dò offline nếu DB bị lộ.
    /// </summary>
    public string OtpHash { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public int AttemptCount { get; set; }

    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// SHA-256 hash của registration token cấp sau khi verify-otp thành công — cầu nối sang bước complete, hiệu lực 10 phút, dùng một lần.
    /// </summary>
    public string? VerificationTokenHash { get; set; }

    public DateTime? VerificationTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
