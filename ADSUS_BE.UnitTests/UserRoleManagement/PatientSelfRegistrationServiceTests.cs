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
    private readonly Mock<IFirebasePhoneVerificationService> _firebase = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly PatientSelfRegistrationService _sut;

    public PatientSelfRegistrationServiceTests()
    {
        _sut = new PatientSelfRegistrationService(
            _users.Object, _firebase.Object, _auth.Object,
            Mock.Of<ILogger<PatientSelfRegistrationService>>());
    }

    private static CompleteRegistrationRequest ValidRequest() => new(
        FirebaseIdToken: "valid-firebase-id-token",
        FullName: "Nguyễn Thị Lan",
        Password: "Password123",
        ConfirmPassword: "Password123",
        Email: null,
        DateOfBirth: null);

    [Fact]
    public async Task CompleteRegistrationAsync_ValidToken_CreatesPatientAccountAndLogsIn()
    {
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                "valid-firebase-id-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
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

        var response = await _sut.CompleteRegistrationAsync(
            ValidRequest(), TestContext.Current.CancellationToken);

        Assert.Same(expectedLogin, response);
        Assert.Equal(UserRole.Patient, saved!.Role);
        Assert.Equal(UserStatus.Active, saved.Status);
        Assert.False(saved.MustChangePassword);
        Assert.Equal("0987654321", saved.Phone);
        Assert.Equal(GenderType.Female, saved.Gender);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_InvalidFirebaseToken_ThrowsBusinessException()
    {
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await Assert.ThrowsAsync<BusinessException>(
            () => _sut.CompleteRegistrationAsync(ValidRequest(), TestContext.Current.CancellationToken));

        _users.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CompleteRegistrationAsync_PhoneAlreadyRegistered_ThrowsConflictException()
    {
        _firebase.Setup(f => f.VerifyAndGetLocalPhoneNumberAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("0987654321");
        _users.Setup(r => r.PhoneExistsAsync("0987654321", It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(
            () => _sut.CompleteRegistrationAsync(ValidRequest(), TestContext.Current.CancellationToken));
    }
}
