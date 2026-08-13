using ADSUS_BE.BLL.UserRoleManagement.Mappers;
using ADSUS_BE.DAL.Entities;
using Xunit;

namespace ADSUS_BE.UnitTests.UserRoleManagement;

/// <summary>
/// Thêm 12/08/2026 (P12, sau khi UserAccountMapper được tách ra ở P11 review Module 2) —
/// trước đó ToResponse là private static method ngay trong UserAccountService, không có test
/// Mapper riêng. Coverage target cho Mapper là 100% (unit-test-convention.md §1).
/// </summary>
public class UserAccountMapperTests
{
    [Fact]
    public void ToResponse_MapsAllFieldsFromUser()
    {
        // Arrange — vai trò không phải PATIENT nên ngày sinh phải được ánh xạ bình thường.
        var user = BuildUser();
        user.DateOfBirth = new DateOnly(1985, 3, 10);

        // Act
        var result = UserAccountMapper.ToResponse(user, Guid.NewGuid());

        // Assert
        Assert.Equal(user.UserId, result.UserId);
        Assert.Equal(user.Phone, result.PhoneNumber);
        Assert.Equal(user.FullName, result.FullName);
        Assert.Equal(user.Email, result.Email);
        Assert.Equal("DOCTOR", result.Role);
        Assert.Equal("ACTIVE", result.Status);
        Assert.Equal("1985-03-10", result.DateOfBirth);
        Assert.Equal(user.MustChangePassword, result.MustChangePassword);
        Assert.Equal(user.CreatedAt, result.CreatedAt);
    }

    [Theory]
    [InlineData(UserRole.Admin, "ADMIN")]
    [InlineData(UserRole.Doctor, "DOCTOR")]
    [InlineData(UserRole.Nurse, "NURSE")]
    [InlineData(UserRole.Patient, "PATIENT")]
    public void ToResponse_RoleIsUppercaseApiString(UserRole role, string expected)
    {
        var user = BuildUser();
        user.Role = role;

        var result = UserAccountMapper.ToResponse(user, Guid.NewGuid());

        Assert.Equal(expected, result.Role);
    }

    [Theory]
    [InlineData(UserStatus.Active, "ACTIVE")]
    [InlineData(UserStatus.Deactivated, "DEACTIVATED")]
    public void ToResponse_StatusIsUppercaseApiString(UserStatus status, string expected)
    {
        var user = BuildUser();
        user.Status = status;

        var result = UserAccountMapper.ToResponse(user, Guid.NewGuid());

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void ToResponse_PatientRole_DateOfBirthIsAlwaysNull()
    {
        // BR-01 — kể cả khi trong DB có sẵn ngày sinh (dữ liệu cũ, hoặc vai trò vừa bị đổi từ
        // Bác sĩ/Điều dưỡng sang Bệnh nhân), giao diện quản trị vẫn không được thấy.
        var user = BuildUser();
        user.Role = UserRole.Patient;
        user.DateOfBirth = new DateOnly(1990, 5, 20);

        var result = UserAccountMapper.ToResponse(user, Guid.NewGuid());

        Assert.Null(result.DateOfBirth);
    }

    [Fact]
    public void ToResponse_NonPatientRole_NullDateOfBirth_MapsToNull()
    {
        var user = BuildUser();
        user.DateOfBirth = null;

        var result = UserAccountMapper.ToResponse(user, Guid.NewGuid());

        Assert.Null(result.DateOfBirth);
    }

    [Fact]
    public void ToResponse_NullEmail_MapsToNull()
    {
        var user = BuildUser();
        user.Email = null;

        var result = UserAccountMapper.ToResponse(user, Guid.NewGuid());

        Assert.Null(result.Email);
    }

    [Fact]
    public void ToResponse_ActingAdminIdMatchesUserId_IsCurrentUserIsTrue()
    {
        // Để giao diện ẩn nút khoá/vô hiệu hoá trên đúng dòng của người đang xem (UC-04 AF-04).
        var user = BuildUser();

        var result = UserAccountMapper.ToResponse(user, user.UserId);

        Assert.True(result.IsCurrentUser);
    }

    [Fact]
    public void ToResponse_ActingAdminIdDiffersFromUserId_IsCurrentUserIsFalse()
    {
        var user = BuildUser();

        var result = UserAccountMapper.ToResponse(user, Guid.NewGuid());

        Assert.False(result.IsCurrentUser);
    }

    private static User BuildUser() => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0900000001",
        FullName = "Nguyễn Văn A",
        Email = "test@adsus.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
        Status = UserStatus.Active,
        Role = UserRole.Doctor,
        MustChangePassword = false,
        CreatedAt = DateTime.UtcNow,
    };
}
