using System.Security.Cryptography;
using System.Text;
using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Common.Exceptions;
using ADSUS_BE.BLL.UserRoleManagement.DTOs;
using ADSUS_BE.BLL.UserRoleManagement.Interfaces;
using ADSUS_BE.BLL.UserRoleManagement.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

public class PasswordResetOtpServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPatientRegistrationOtpRepository> _otps = new();
    private readonly Mock<IOtpSmsService> _sms = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly PasswordResetOtpService _sut;

    public PasswordResetOtpServiceTests()
    {
        _sut = new PasswordResetOtpService(
            _users.Object, _otps.Object, _sms.Object, _auth.Object,
            Mock.Of<ILogger<PasswordResetOtpService>>());
    }

    private static User ActivePatient(string phone) => new()
    {
        UserId = Guid.NewGuid(),
        Phone = phone,
        FullName = "Nguyễn Thị Lan",
        Role = UserRole.Patient,
        Status = UserStatus.Active,
        PasswordHash = "irrelevant",
    };

    [Fact]
    public async Task RequestOtpAsync_ActivePatientPhone_SendsSmsAndReturnsSuccess()
    {
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(ActivePatient("0987654321"));
        _sms.Setup(s => s.SendOtpAsync("0987654321", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.RequestOtpAsync(
            new RequestPasswordResetOtpRequest("0987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetOtpResult.Success, result);
        _sms.Verify(s => s.SendOtpAsync("0987654321", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestOtpAsync_PhoneNotFound_ReturnsPhoneNotFoundWithoutSendingSms()
    {
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var result = await _sut.RequestOtpAsync(
            new RequestPasswordResetOtpRequest("0987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetOtpResult.PhoneNotFound, result);
        _sms.Verify(s => s.SendOtpAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestOtpAsync_PhoneBelongsToNonPatientRole_ReturnsPhoneNotFound()
    {
        // Chỉ Patient (Global Constraints) — Admin/Doctor/Staff/Pharmacist coi như "không tìm
        // thấy" ở đường này, KHÔNG disclose thêm gì.
        var doctor = ActivePatient("0987654321");
        doctor.Role = UserRole.Doctor;
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(doctor);

        var result = await _sut.RequestOtpAsync(
            new RequestPasswordResetOtpRequest("0987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetOtpResult.PhoneNotFound, result);
    }

    [Fact]
    public async Task RequestOtpAsync_DeactivatedPatient_ReturnsPhoneNotFound()
    {
        var deactivated = ActivePatient("0987654321");
        deactivated.Status = UserStatus.Deactivated;
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(deactivated);

        var result = await _sut.RequestOtpAsync(
            new RequestPasswordResetOtpRequest("0987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetOtpResult.PhoneNotFound, result);
    }

    [Fact]
    public async Task RequestOtpAsync_RequestedLessThanSixtySecondsAgo_ReturnsTooSoon()
    {
        // Sửa 13/09/2026 (review Task 7): eligibility check giờ chạy TRƯỚC cooldown check
        // (xem PasswordResetOtpService.RequestOtpAsync) — số phải là Patient Active hợp lệ để
        // test này thật sự chạm tới nhánh cooldown, không bị chặn sớm bởi PhoneNotFound.
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(ActivePatient("0987654321"));

        var recentOtp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(), Phone = "0987654321", OtpHash = "irrelevant",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), CreatedAt = DateTime.UtcNow.AddSeconds(-10),
        };
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync(recentOtp);

        var result = await _sut.RequestOtpAsync(
            new RequestPasswordResetOtpRequest("0987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(PasswordResetOtpResult.TooSoon, result);
    }

    [Fact]
    public async Task VerifyOtpAsync_CorrectCode_ReturnsSuccessWithResetToken()
    {
        const string code = "123456";
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(), Phone = "0987654321", OtpHash = Sha256Base64(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), AttemptCount = 0, CreatedAt = DateTime.UtcNow,
        };
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        var result = await _sut.VerifyOtpAsync(
            new VerifyPasswordResetOtpRequest("0987654321", code), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ResetToken));
    }

    [Fact]
    public async Task VerifyOtpAsync_WrongCode_Fails()
    {
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(), Phone = "0987654321", OtpHash = Sha256Base64("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5), AttemptCount = 0, CreatedAt = DateTime.UtcNow,
        };
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        var result = await _sut.VerifyOtpAsync(
            new VerifyPasswordResetOtpRequest("0987654321", "000000"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal(1, otp.AttemptCount);
    }

    [Fact]
    public async Task VerifyOtpAsync_NoOtpRequestedForPhone_Fails()
    {
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);

        var result = await _sut.VerifyOtpAsync(
            new VerifyPasswordResetOtpRequest("0987654321", "123456"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CompleteAsync_ValidToken_UpdatesPasswordAndLogsIn()
    {
        const string plainToken = "opaque-reset-token";
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(), Phone = "0987654321", OtpHash = "irrelevant",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            VerifiedAt = DateTime.UtcNow,
            VerificationTokenHash = Sha256Base64(plainToken),
            VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-11),
        };
        _otps.Setup(r => r.GetByVerificationTokenHashAsync(Sha256Base64(plainToken), It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        var patient = ActivePatient("0987654321");
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(patient);
        _users.Setup(r => r.GetForUpdateAsync(patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(patient);

        var expectedLogin = new LoginResponse { AccessToken = "token-abc", UserId = patient.UserId };
        _auth.Setup(a => a.LoginAsync(
                It.Is<LoginRequest>(r => r.PhoneNumber == "0987654321" && r.Password == "NewPassword123"),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(expectedLogin);

        var request = new CompletePasswordResetWithOtpRequest(plainToken, "NewPassword123", "NewPassword123");

        var response = await _sut.CompleteAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(expectedLogin, response);
        Assert.False(patient.MustChangePassword);
    }

    [Fact]
    public async Task CompleteAsync_InvalidToken_ThrowsBusinessException()
    {
        _otps.Setup(r => r.GetByVerificationTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);

        var request = new CompletePasswordResetWithOtpRequest("bogus-token", "NewPassword123", "NewPassword123");

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteAsync_AccountDeactivatedMeanwhile_ThrowsBusinessException()
    {
        const string plainToken = "opaque-reset-token";
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(), Phone = "0987654321", OtpHash = "irrelevant",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            VerifiedAt = DateTime.UtcNow,
            VerificationTokenHash = Sha256Base64(plainToken),
            VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-11),
        };
        _otps.Setup(r => r.GetByVerificationTokenHashAsync(Sha256Base64(plainToken), It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        var deactivated = ActivePatient("0987654321");
        deactivated.Status = UserStatus.Deactivated;
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(deactivated);

        var request = new CompletePasswordResetWithOtpRequest(plainToken, "NewPassword123", "NewPassword123");

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteAsync(request, TestContext.Current.CancellationToken));
    }

    private static string Sha256Base64(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(hash);
    }
}
