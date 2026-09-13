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
    private readonly Mock<IFirebasePhoneVerificationService> _firebase = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly PasswordResetOtpService _sut;

    public PasswordResetOtpServiceTests()
    {
        _sut = new PasswordResetOtpService(
            _users.Object, _firebase.Object, _auth.Object,
            Mock.Of<ILogger<PasswordResetOtpService>>());
    }

    private static User ActivePatient(string phone) => new()
    {
        UserId = Guid.NewGuid(), Phone = phone, FullName = "Nguyễn Thị Lan",
        Role = UserRole.Patient, Status = UserStatus.Active, PasswordHash = "irrelevant",
    };

    private static CompletePasswordResetWithFirebaseRequest ValidRequest() => new(
        FirebaseIdToken: "valid-firebase-id-token",
        NewPassword: "NewPassword123",
        ConfirmNewPassword: "NewPassword123");

    [Fact]
    public async Task CompleteAsync_ActivePatientPhone_UpdatesPasswordAndLogsIn()
    {
        var patient = ActivePatient("0987654321");
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                "valid-firebase-id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(patient);
        _users.Setup(r => r.GetForUpdateAsync(patient.UserId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(patient);

        var expectedLogin = new LoginResponse { AccessToken = "token-abc", UserId = patient.UserId };
        _auth.Setup(a => a.LoginAsync(
                It.Is<LoginRequest>(r => r.PhoneNumber == "0987654321" && r.Password == "NewPassword123"),
                It.IsAny<CancellationToken>()))
             .ReturnsAsync(expectedLogin);

        var response = await _sut.CompleteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Same(expectedLogin, response);
        Assert.False(patient.MustChangePassword);
    }

    [Fact]
    public async Task CompleteAsync_InvalidFirebaseToken_ThrowsBusinessException()
    {
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteAsync(ValidRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CompleteAsync_PhoneNotFound_ReturnsNull()
    {
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync((User?)null);

        var result = await _sut.CompleteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteAsync_PhoneBelongsToNonPatientRole_ReturnsNull()
    {
        var doctor = ActivePatient("0987654321");
        doctor.Role = UserRole.Doctor;
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(doctor);

        var result = await _sut.CompleteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task CompleteAsync_PhoneBelongsToDeactivatedPatient_ReturnsNull()
    {
        var deactivated = ActivePatient("0987654321");
        deactivated.Status = UserStatus.Deactivated;
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.GetByPhoneReadOnlyAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(deactivated);

        var result = await _sut.CompleteAsync(ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Null(result);
    }
}
