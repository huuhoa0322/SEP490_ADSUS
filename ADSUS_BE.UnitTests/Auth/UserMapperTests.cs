using ADSUS_BE.BLL.Auth.Mappers;
using ADSUS_BE.DAL.Entities;
using Xunit;

namespace ADSUS_BE.UnitTests.Auth;

/// <summary>
/// Thêm 12/08/2026 (P12, sau khi UserMapper được tách ra ở P11 review) — trước đó
/// LoginResponse/UserProfileResponse dựng trực tiếp trong Service nên không có test Mapper
/// riêng. Coverage target cho Mapper là 100% (unit-test-convention.md §1).
/// </summary>
public class UserMapperTests
{
    [Fact]
    public void ToLoginResponse_MapsAllFieldsFromUserAndAccessToken()
    {
        // Arrange
        var user = BuildUser();

        // Act
        var result = UserMapper.ToLoginResponse(user, "fake.jwt.token");

        // Assert
        Assert.Equal("fake.jwt.token", result.AccessToken);
        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal("DOCTOR", result.Role);
        Assert.Equal(user.FullName, result.FullName);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal(user.MustChangePassword, result.MustChangePassword);
    }

    [Theory]
    [InlineData(UserRole.Admin, "ADMIN")]
    [InlineData(UserRole.Doctor, "DOCTOR")]
    [InlineData(UserRole.Nurse, "NURSE")]
    [InlineData(UserRole.Patient, "PATIENT")]
    public void ToLoginResponse_RoleIsUppercaseApiString(UserRole role, string expected)
    {
        var user = BuildUser();
        user.Role = role;

        var result = UserMapper.ToLoginResponse(user, "token");

        Assert.Equal(expected, result.Role);
    }

    [Fact]
    public void ToLoginResponse_NullEmail_MapsToNull()
    {
        var user = BuildUser();
        user.Email = null;

        var result = UserMapper.ToLoginResponse(user, "token");

        Assert.Null(result.Email);
    }

    [Fact]
    public void ToProfileResponse_MapsAllFieldsFromUser()
    {
        // Arrange
        var user = BuildUser();
        user.DateOfBirth = new DateOnly(1990, 5, 20);
        user.BiometricEnabled = true;
        user.MustChangePassword = true;

        // Act
        var result = UserMapper.ToProfileResponse(user);

        // Assert
        Assert.Equal(user.FullName, result.FullName);
        Assert.Equal(user.Phone, result.PhoneNumber);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal("1990-05-20", result.DateOfBirth);
        Assert.Equal("DOCTOR", result.Role);
        Assert.True(result.BiometricEnabled);
        Assert.True(result.MustChangePassword);
    }

    [Fact]
    public void ToProfileResponse_DateOfBirthIsNull_MapsToNull()
    {
        // BR-01 — DateOfBirth luôn null với vai trò PATIENT ở tầng Service; Mapper chỉ phản
        // ánh đúng những gì Entity đưa vào, không tự áp luật đó.
        var user = BuildUser();
        user.DateOfBirth = null;

        var result = UserMapper.ToProfileResponse(user);

        Assert.Null(result.DateOfBirth);
    }

    private static User BuildUser() => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0900000001",
        FullName = "Test User",
        Email = "test@adsus.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
        Status = UserStatus.Active,
        Role = UserRole.Doctor,
        BiometricEnabled = false,
        MustChangePassword = false,
    };
}
