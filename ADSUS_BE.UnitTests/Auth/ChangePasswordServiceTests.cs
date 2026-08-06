using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Interfaces;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.DAL.Entities;
using ADSUS_BE.DAL.Repositories.Interfaces;
using Moq;
using Xunit;

namespace ADSUS_BE.UnitTests.Auth;

/// <summary>
/// UC-25 change own password.
///
/// BR-01: the change succeeds only when the supplied current password is correct.
/// Clearing must_change_password on success is what closes the admin-issued temporary
/// password loop from UC-03 / UC-04.
///
/// Unlike sign-in, naming the exact failure is allowed here — the caller is already
/// authenticated and acting on their own account.
/// </summary>
public class ChangePasswordServiceTests
{
    private const string CurrentPassword = "Test@123";
    private const string NewPassword = "NewPass1";

    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IJwtTokenService> _tokens = new();
    private readonly AuthService _sut;

    public ChangePasswordServiceTests()
    {
        _sut = new AuthService(_users.Object, _tokens.Object);
    }

    [Fact]
    public async Task ChangePasswordAsync_CorrectCurrentPassword_ReturnsSuccess()
    {
        var user = BuildUser(UserStatus.Active);
        SetupUser(user);

        var result = await _sut.ChangePasswordAsync(user.UserId, Request(CurrentPassword));

        Assert.Equal(ChangePasswordResult.Success, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_PersistsTheNewHash()
    {
        var user = BuildUser(UserStatus.Active);
        var originalHash = user.PasswordHash;
        SetupUser(user);

        await _sut.ChangePasswordAsync(user.UserId, Request(CurrentPassword));

        Assert.NotEqual(originalHash, user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash));
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_ClearsMustChangePasswordFlag()
    {
        // This is the step that ends the forced-change state after an admin reset.
        var user = BuildUser(UserStatus.Active);
        user.MustChangePassword = true;
        SetupUser(user);

        await _sut.ChangePasswordAsync(user.UserId, Request(CurrentPassword));

        Assert.False(user.MustChangePassword);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsCurrentPasswordIncorrect()
    {
        var user = BuildUser(UserStatus.Active);
        SetupUser(user);

        var result = await _sut.ChangePasswordAsync(user.UserId, Request("WrongOne1"));

        Assert.Equal(ChangePasswordResult.CurrentPasswordIncorrect, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_LeavesPasswordUnchanged()
    {
        var user = BuildUser(UserStatus.Active);
        var originalHash = user.PasswordHash;
        SetupUser(user);

        await _sut.ChangePasswordAsync(user.UserId, Request("WrongOne1"));

        Assert.Equal(originalHash, user.PasswordHash);
        _users.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangePasswordAsync_MustChangePassword_WrongCurrentPassword_StillSucceeds()
    {
        // Sửa 06/08/2026 — tài khoản còn đang dùng mật khẩu tạm (do Admin/Điều dưỡng cấp) thì
        // không cần xác thực CurrentPassword nữa: người dùng vừa chứng minh biết giá trị đó
        // qua bước đăng nhập ngay trước đây. Cố tình gửi CurrentPassword SAI vẫn phải thành
        // công, để không ai có thể "khoá" luồng này bằng cách âm thầm gửi giá trị đúng thật.
        var user = BuildUser(UserStatus.Active);
        user.MustChangePassword = true;
        SetupUser(user);

        var result = await _sut.ChangePasswordAsync(user.UserId, Request("DefinitelyWrong1"));

        Assert.Equal(ChangePasswordResult.Success, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_MustChangePassword_NullCurrentPassword_StillSucceeds()
    {
        var user = BuildUser(UserStatus.Active);
        user.MustChangePassword = true;
        SetupUser(user);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = null,
            NewPassword = NewPassword,
            ConfirmNewPassword = NewPassword,
        };

        var result = await _sut.ChangePasswordAsync(user.UserId, request);

        Assert.Equal(ChangePasswordResult.Success, result);
        Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, user.PasswordHash));
    }

    [Fact]
    public async Task ChangePasswordAsync_NotMustChangePassword_NullCurrentPassword_ReturnsCurrentPasswordIncorrect()
    {
        // Đổi mật khẩu tự nguyện (không bị ép) — CurrentPassword vẫn bắt buộc và phải đúng,
        // hành vi không đổi so với trước.
        var user = BuildUser(UserStatus.Active);
        SetupUser(user);

        var request = new ChangePasswordRequest
        {
            CurrentPassword = null,
            NewPassword = NewPassword,
            ConfirmNewPassword = NewPassword,
        };

        var result = await _sut.ChangePasswordAsync(user.UserId, request);

        Assert.Equal(ChangePasswordResult.CurrentPasswordIncorrect, result);
    }

    [Fact]
    public async Task ChangePasswordAsync_UserNoLongerExists_ReturnsUserNotFound()
    {
        SetupUser(null);

        var result = await _sut.ChangePasswordAsync(Guid.NewGuid(), Request(CurrentPassword));

        Assert.Equal(ChangePasswordResult.UserNotFound, result);
    }

    [Theory]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.Deactivated)]
    public async Task ChangePasswordAsync_AccountNotActive_IsRejected(UserStatus status)
    {
        // The token may still be valid while an admin locked the account in the meantime.
        var user = BuildUser(status);
        SetupUser(user);

        var result = await _sut.ChangePasswordAsync(user.UserId, Request(CurrentPassword));

        Assert.Equal(ChangePasswordResult.AccountNotActive, result);
    }

    // ---- helpers ----

    private void SetupUser(User? user) =>
        _users.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(user);

    private static ChangePasswordRequest Request(string currentPassword) => new()
    {
        CurrentPassword = currentPassword,
        NewPassword = NewPassword,
        ConfirmNewPassword = NewPassword,
    };

    private static User BuildUser(UserStatus status) => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0900000001",
        FullName = "Test User",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(CurrentPassword),
        Status = status,
        Role = UserRole.Doctor,
        MustChangePassword = false,
    };
}
