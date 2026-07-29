using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Auth;

/// <summary>
/// UC-01 sign-in rules.
///
/// BR-01: sign-in succeeds only when the phone number exists, the password is correct AND
///        the account status is Active.
/// GB-06: every failure must be indistinguishable from the outside. The service enforces
///        this by returning LoginResponse? — null carries no reason at all.
/// BR-03: the response carries the role so the client can route the user.
/// </summary>
public class AuthServiceTests
{
    private const string CorrectPassword = "Test@123";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IJwtTokenService> _tokens = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("fake.jwt.token");
        _sut = new AuthService(_users.Object, _tokens.Object);
    }

    [Fact]
    public async Task LoginAsync_ActiveAccountWithCorrectPassword_ReturnsResponse()
    {
        // Arrange
        SetupUser(BuildUser(UserStatus.Active, UserRole.Admin));

        // Act
        var result = await _sut.LoginAsync(Request(CorrectPassword));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("ADMIN", result!.Role);
        Assert.Equal("fake.jwt.token", result.AccessToken);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        SetupUser(BuildUser(UserStatus.Active, UserRole.Doctor));

        var result = await _sut.LoginAsync(Request("WrongPassword1"));

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_PhoneNumberNotFound_ReturnsNull()
    {
        // Repository returns null — no account with that phone number.
        SetupUser(null);

        var result = await _sut.LoginAsync(Request(CorrectPassword));

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ReturnsNullEvenWithCorrectPassword()
    {
        // BR-01: a correct password is not enough — status must be Active.
        SetupUser(BuildUser(UserStatus.Locked, UserRole.Doctor));

        var result = await _sut.LoginAsync(Request(CorrectPassword));

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_DeactivatedAccount_ReturnsNullEvenWithCorrectPassword()
    {
        SetupUser(BuildUser(UserStatus.Deactivated, UserRole.Doctor));

        var result = await _sut.LoginAsync(Request(CorrectPassword));

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_NoTokenIsIssuedWhenSignInFails()
    {
        // A token must never be minted for a rejected sign-in.
        SetupUser(BuildUser(UserStatus.Locked, UserRole.Doctor));

        await _sut.LoginAsync(Request(CorrectPassword));

        _tokens.Verify(t => t.GenerateAccessToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_MustChangePasswordFlag_IsPassedThrough()
    {
        // UC-25 depends on this flag reaching the client.
        var user = BuildUser(UserStatus.Active, UserRole.Doctor);
        user.MustChangePassword = true;
        SetupUser(user);

        var result = await _sut.LoginAsync(Request(CorrectPassword));

        Assert.NotNull(result);
        Assert.True(result!.MustChangePassword);
    }

    [Theory]
    [InlineData(UserRole.Admin, "ADMIN")]
    [InlineData(UserRole.Doctor, "DOCTOR")]
    [InlineData(UserRole.Patient, "PATIENT")]
    public async Task LoginAsync_RoleIsReturnedInUppercase(UserRole role, string expected)
    {
        // The client and the database both use uppercase labels; C# uses PascalCase.
        SetupUser(BuildUser(UserStatus.Active, role));

        var result = await _sut.LoginAsync(Request(CorrectPassword));

        Assert.Equal(expected, result!.Role);
    }

    // ---- helpers ----

    private void SetupUser(User? user) =>
        _users.Setup(r => r.GetByPhoneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

    private static LoginRequest Request(string password) =>
        new() { PhoneNumber = "0900000001", Password = password };

    private static User BuildUser(UserStatus status, UserRole role) => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0900000001",
        FullName = "Test User",
        Email = "test@adsus.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(CorrectPassword),
        Status = status,
        Role = role,
        MustChangePassword = false,
    };
}
