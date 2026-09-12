using System;
using System.Threading;
using System.Threading.Tasks;
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

public class PatientSelfRegistrationServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPatientRegistrationOtpRepository> _otps = new();
    private readonly Mock<IOtpSmsService> _sms = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly PatientSelfRegistrationService _sut;

    public PatientSelfRegistrationServiceTests()
    {
        _sut = new PatientSelfRegistrationService(
            _users.Object,
            _otps.Object,
            _sms.Object,
            _auth.Object,
            Mock.Of<ILogger<PatientSelfRegistrationService>>());
    }

    [Fact]
    public async Task RequestOtpAsync_NewPhone_SendsSmsAndReturnsSuccess()
    {
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        _sms.Setup(s => s.SendOtpAsync("0987654321", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.RequestOtpAsync(
            new RequestRegistrationOtpRequest("0987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationOtpResult.Success, result);
        _sms.Verify(s => s.SendOtpAsync("0987654321", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _otps.Verify(r => r.CreateAsync(It.IsAny<PatientRegistrationOtp>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RequestOtpAsync_PhoneAlreadyRegistered_ReturnsPhoneAlreadyRegisteredAndDoesNotSendSms()
    {
        // Báo rõ cho client (xem Global Constraints) — KHÔNG còn trả Success mơ hồ như bản nháp đầu.
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        var result = await _sut.RequestOtpAsync(
            new RequestRegistrationOtpRequest("0987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationOtpResult.PhoneAlreadyRegistered, result);
        _sms.Verify(s => s.SendOtpAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _otps.Verify(r => r.CreateAsync(It.IsAny<PatientRegistrationOtp>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RequestOtpAsync_RequestedLessThanSixtySecondsAgo_ReturnsTooSoon()
    {
        var recentOtp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = "0987654321",
            OtpHash = "irrelevant",
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            CreatedAt = DateTime.UtcNow.AddSeconds(-10),
        };
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync(recentOtp);

        var result = await _sut.RequestOtpAsync(
            new RequestRegistrationOtpRequest("0987654321"), TestContext.Current.CancellationToken);

        Assert.Equal(RegistrationOtpResult.TooSoon, result);
        _sms.Verify(s => s.SendOtpAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyOtpAsync_CorrectCode_ReturnsSuccessWithRegistrationToken()
    {
        const string code = "123456";
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = "0987654321",
            OtpHash = Sha256Base64(code),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        var result = await _sut.VerifyOtpAsync(
            new VerifyRegistrationOtpRequest("0987654321", code), TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.RegistrationToken));
        Assert.NotNull(otp.VerifiedAt);
        Assert.NotNull(otp.VerificationTokenHash);
    }

    [Fact]
    public async Task VerifyOtpAsync_WrongCode_IncrementsAttemptAndFails()
    {
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = "0987654321",
            OtpHash = Sha256Base64("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow,
        };
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        var result = await _sut.VerifyOtpAsync(
            new VerifyRegistrationOtpRequest("0987654321", "000000"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Null(result.RegistrationToken);
        Assert.Equal(1, otp.AttemptCount);
    }

    [Fact]
    public async Task VerifyOtpAsync_ExpiredCode_Fails()
    {
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = "0987654321",
            OtpHash = Sha256Base64("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow.AddMinutes(-6),
        };
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        var result = await _sut.VerifyOtpAsync(
            new VerifyRegistrationOtpRequest("0987654321", "123456"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task VerifyOtpAsync_NoOtpRequestedForPhone_Fails()
    {
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);

        var result = await _sut.VerifyOtpAsync(
            new VerifyRegistrationOtpRequest("0987654321", "123456"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task VerifyOtpAsync_AlreadyAtMaxAttempts_FailsWithoutCheckingCode()
    {
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = "0987654321",
            OtpHash = Sha256Base64("123456"),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5),
            AttemptCount = 5,
            CreatedAt = DateTime.UtcNow,
        };
        _otps.Setup(r => r.GetLatestByPhoneAsync("0987654321", It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        // Gõ ĐÚNG mã nhưng đã hết lượt thử — vẫn phải fail.
        var result = await _sut.VerifyOtpAsync(
            new VerifyRegistrationOtpRequest("0987654321", "123456"), TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_ValidToken_CreatesPatientAccountAndLogsIn()
    {
        const string plainToken = "opaque-registration-token";
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = "0987654321",
            OtpHash = "irrelevant",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10), // OTP gốc đã hết hạn, KHÔNG còn quan trọng
            VerifiedAt = DateTime.UtcNow,
            VerificationTokenHash = Sha256Base64(plainToken),
            VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-11),
        };
        _otps.Setup(r => r.GetByVerificationTokenHashAsync(Sha256Base64(plainToken), It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);

        User? saved = null;
        _users.Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
              .Callback<User, CancellationToken>((u, _) => saved = u)
              .Returns(Task.CompletedTask);

        var expectedLogin = new LoginResponse { AccessToken = "token-abc", UserId = Guid.NewGuid() };
        _auth.Setup(a => a.LoginAsync(
                It.Is<LoginRequest>(r => r.PhoneNumber == "0987654321" && r.Password == "Password123"),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(expectedLogin);

        var request = new CompleteRegistrationRequest(
            RegistrationToken: plainToken,
            FullName: "Nguyễn Thị Lan",
            Password: "Password123",
            ConfirmPassword: "Password123",
            Email: null,
            DateOfBirth: null);

        var response = await _sut.CompleteRegistrationAsync(request, TestContext.Current.CancellationToken);

        Assert.Same(expectedLogin, response);
        Assert.Equal(UserRole.Patient, saved!.Role);
        Assert.Equal(UserStatus.Active, saved.Status);
        Assert.False(saved.MustChangePassword); // Tự chọn mật khẩu — không cần ép đổi lại.
    }

    [Fact]
    public async Task CompleteRegistrationAsync_InvalidToken_ThrowsBusinessException()
    {
        _otps.Setup(r => r.GetByVerificationTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ReturnsAsync((PatientRegistrationOtp?)null);

        var request = new CompleteRegistrationRequest(
            "bogus-token", "Nguyễn Thị Lan", "Password123", "Password123", null, null);

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteRegistrationAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteRegistrationAsync_ExpiredToken_ThrowsBusinessException()
    {
        const string plainToken = "opaque-registration-token";
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = "0987654321",
            OtpHash = "irrelevant",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-20),
            VerifiedAt = DateTime.UtcNow.AddMinutes(-15),
            VerificationTokenHash = Sha256Base64(plainToken),
            VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(-5), // đã hết hạn
            CreatedAt = DateTime.UtcNow.AddMinutes(-21),
        };
        _otps.Setup(r => r.GetByVerificationTokenHashAsync(Sha256Base64(plainToken), It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);

        var request = new CompleteRegistrationRequest(
            plainToken, "Nguyễn Thị Lan", "Password123", "Password123", null, null);

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteRegistrationAsync(request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteRegistrationAsync_PhoneRegisteredMeanwhile_ThrowsConflictException()
    {
        // Race condition hiếm: một đường khác (Admin/Điều dưỡng tạo hộ) đăng ký đúng số này
        // giữa lúc verify-otp và complete.
        const string plainToken = "opaque-registration-token";
        var otp = new PatientRegistrationOtp
        {
            OtpId = Guid.NewGuid(),
            Phone = "0987654321",
            OtpHash = "irrelevant",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            VerifiedAt = DateTime.UtcNow,
            VerificationTokenHash = Sha256Base64(plainToken),
            VerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow.AddMinutes(-11),
        };
        _otps.Setup(r => r.GetByVerificationTokenHashAsync(Sha256Base64(plainToken), It.IsAny<CancellationToken>()))
             .ReturnsAsync(otp);
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        var request = new CompleteRegistrationRequest(
            plainToken, "Nguyễn Thị Lan", "Password123", "Password123", null, null);

        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.CompleteRegistrationAsync(request, TestContext.Current.CancellationToken));
    }

    private static string Sha256Base64(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToBase64String(hash);
    }
}
