using ADSUS_BE.DAL.Entities;

namespace ADSUS_BE.DAL.Repositories.Interfaces;

public interface IPatientRegistrationOtpRepository
{
    Task CreateAsync(PatientRegistrationOtp otp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hàng OTP MỚI NHẤT của một số điện thoại (theo CreatedAt), bất kể còn hiệu lực hay
    /// không — dùng cả cho chống spam (kiểm CreatedAt) và cho verify-otp (kiểm ExpiresAt/
    /// AttemptCount). Trả null nếu số đó chưa từng xin OTP.
    /// </summary>
    Task<PatientRegistrationOtp?> GetLatestByPhoneAsync(string phone, CancellationToken cancellationToken = default);

    Task<PatientRegistrationOtp?> GetByVerificationTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
