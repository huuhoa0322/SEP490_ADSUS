using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
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
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _tokens.Setup(t => t.GenerateAccessToken(It.IsAny<User>())).Returns("fake.jwt.token");
        _sut = new AuthService(_users.Object, _refreshTokens.Object, _tokens.Object, new Mock<ILogger<AuthService>>().Object);
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
        SetupUser(BuildUser(UserStatus.Deactivated, UserRole.Doctor));

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
    [InlineData(UserRole.Nurse, "NURSE")]
    public async Task LoginAsync_RoleIsReturnedInUppercase(UserRole role, string expected)
    {
        // The client and the database both use uppercase labels; C# uses PascalCase.
        SetupUser(BuildUser(UserStatus.Active, role));

        var result = await _sut.LoginAsync(Request(CorrectPassword));

        Assert.Equal(expected, result!.Role);
    }

    // ---- helpers ----

    private void SetupUser(User? user) =>
        _users.Setup(r => r.GetByPhoneReadOnlyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
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

    #region RefreshTokensAsync Tests

    [Fact]
    public async Task RefreshTokensAsync_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        var user = BuildUser(UserStatus.Active, UserRole.Patient);
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = "somehash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            DeviceInfo = "Test Device"
        };

        _refreshTokens.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);
        _users.Setup(r => r.GetByIdReadOnlyAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _refreshTokens.Setup(r => r.RevokeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _refreshTokens.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.RefreshTokensAsync("valid_refresh_token");

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result!.AccessToken);
        Assert.NotNull(result.RefreshToken);
        Assert.NotNull(result.ExpiresAt);
    }

    [Fact]
    public async Task RefreshTokensAsync_ExpiredToken_ReturnsNull()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "somehash",
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired
            CreatedAt = DateTime.UtcNow.AddDays(-8),
        };

        _refreshTokens.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        // Act
        var result = await _sut.RefreshTokensAsync("expired_refresh_token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_RevokedToken_ReturnsNull()
    {
        // Arrange
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "somehash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            RevokedAt = DateTime.UtcNow.AddMinutes(-30) // Revoked
        };

        _refreshTokens.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);

        // Act
        var result = await _sut.RefreshTokensAsync("revoked_refresh_token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_UserInactive_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "somehash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        };

        var inactiveUser = BuildUser(UserStatus.Deactivated, UserRole.Patient);
        inactiveUser.UserId = userId;

        _refreshTokens.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);
        _users.Setup(r => r.GetByIdReadOnlyAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveUser);

        // Act
        var result = await _sut.RefreshTokensAsync("valid_token_for_inactive_user");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_TokenNotFound_ReturnsNull()
    {
        // Arrange
        _refreshTokens.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshToken?)null);

        // Act
        var result = await _sut.RefreshTokensAsync("nonexistent_token");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RefreshTokensAsync_RevokesOldToken()
    {
        // Arrange
        var user = BuildUser(UserStatus.Active, UserRole.Patient);
        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            TokenHash = "somehash",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
        };

        _refreshTokens.Setup(r => r.GetByTokenHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedToken);
        _users.Setup(r => r.GetByIdReadOnlyAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _refreshTokens.Setup(r => r.RevokeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _refreshTokens.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RefreshTokensAsync("valid_refresh_token");

        // Assert
        _refreshTokens.Verify(r => r.RevokeAsync(storedToken.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RevokeAllRefreshTokensAsync Tests

    [Fact]
    public async Task RevokeAllRefreshTokensAsync_CallsRepository()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _refreshTokens.Setup(r => r.RevokeAllForUserAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.RevokeAllRefreshTokensAsync(userId);

        // Assert
        _refreshTokens.Verify(r => r.RevokeAllForUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region ChangePasswordAsync Tests

    [Fact]
    public async Task ChangePasswordAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var user = BuildUser(UserStatus.Active, UserRole.Patient);
        user.MustChangePassword = false;
        _users.Setup(r => r.GetForUpdateAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = CorrectPassword,
            NewPassword = "NewPass@123"
        };

        // Act
        var result = await _sut.ChangePasswordAsync(user.UserId, request);

        // Assert
        Assert.Equal(ChangePasswordResult.Success, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNotFound_ReturnsUserNotFound()
    {
        // Arrange
        _users.Setup(r => r.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPass@123",
            NewPassword = "NewPass@123"
        };

        // Act
        var result = await _sut.ChangePasswordAsync(Guid.NewGuid(), request);

        // Assert
        Assert.Equal(ChangePasswordResult.UserNotFound, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_InactiveAccount_ReturnsAccountNotActive()
    {
        // Arrange
        var user = BuildUser(UserStatus.Deactivated, UserRole.Patient);
        _users.Setup(r => r.GetForUpdateAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = CorrectPassword,
            NewPassword = "NewPass@123"
        };

        // Act
        var result = await _sut.ChangePasswordAsync(user.UserId, request);

        // Assert
        Assert.Equal(ChangePasswordResult.AccountNotActive, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsCurrentPasswordIncorrect()
    {
        // Arrange
        var user = BuildUser(UserStatus.Active, UserRole.Patient);
        user.MustChangePassword = false;
        _users.Setup(r => r.GetForUpdateAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword",
            NewPassword = "NewPass@123"
        };

        // Act
        var result = await _sut.ChangePasswordAsync(user.UserId, request);

        // Assert
        Assert.Equal(ChangePasswordResult.CurrentPasswordIncorrect, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_MustChangePasswordFlag_SkipsCurrentPasswordCheck()
    {
        // Arrange
        var user = BuildUser(UserStatus.Active, UserRole.Patient);
        user.MustChangePassword = true; // Must change password
        _users.Setup(r => r.GetForUpdateAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = null, // No current password needed when MustChangePassword is true
            NewPassword = "NewPass@123"
        };

        // Act
        var result = await _sut.ChangePasswordAsync(user.UserId, request);

        // Assert
        Assert.Equal(ChangePasswordResult.Success, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_ClearsMustChangePasswordFlag()
    {
        // Arrange
        var user = BuildUser(UserStatus.Active, UserRole.Patient);
        user.MustChangePassword = true;
        _users.Setup(r => r.GetForUpdateAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new ChangePasswordRequest
        {
            NewPassword = "NewPass@123"
        };

        // Act
        await _sut.ChangePasswordAsync(user.UserId, request);

        // Assert
        Assert.False(user.MustChangePassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_UpdatesPasswordHash()
    {
        // Arrange
        var user = BuildUser(UserStatus.Active, UserRole.Patient);
        user.MustChangePassword = false;
        var originalHash = user.PasswordHash;
        _users.Setup(r => r.GetForUpdateAsync(user.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _users.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = CorrectPassword,
            NewPassword = "NewPass@123"
        };

        // Act
        await _sut.ChangePasswordAsync(user.UserId, request);

        // Assert
        Assert.NotEqual(originalHash, user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPass@123", user.PasswordHash));
    }

    #endregion

    #region LoginAsync - Refresh Token Generation

    [Fact]
    public async Task LoginAsync_Success_GeneratesRefreshToken()
    {
        // Arrange
        SetupUser(BuildUser(UserStatus.Active, UserRole.Patient));
        _refreshTokens.Setup(r => r.CreateAsync(It.IsAny<RefreshToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.LoginAsync(Request(CorrectPassword));

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result!.RefreshToken);
        _refreshTokens.Verify(r => r.CreateAsync(
            It.Is<RefreshToken>(t => t.UserId != Guid.Empty && t.ExpiresAt > DateTime.UtcNow),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
