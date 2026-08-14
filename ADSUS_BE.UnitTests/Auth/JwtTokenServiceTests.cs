using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using ADSUS_BE.BLL.Auth.Services;
using ADSUS_BE.BLL.Common;
using ADSUS_BE.DAL.Entities;
using Microsoft.Extensions.Options;
using Xunit;

namespace ADSUS_BE.UnitTests.Auth;

/// <summary>
/// P12, thêm 14/08/2026 — JwtTokenService trước đó chưa có test riêng, dù claim nó sinh ra
/// (đặc biệt ClaimTypes.Role và ClaimTypes.NameIdentifier) là nền cho toàn bộ [Authorize] và
/// TryGetUserId/TryGetActingAdminId ở khắp các controller — sai một chữ ở đây là hỏng RBAC
/// toàn hệ thống mà không controller nào tự phát hiện được.
/// </summary>
public class JwtTokenServiceTests
{
    // HmacSha256 đòi khoá tối thiểu 256 bit — chuỗi này chỉ để test, không phải khoá thật.
    private const string TestSecretKey = "test-secret-key-at-least-32-characters-long-for-hmac-sha256";

    private readonly JwtTokenService _sut = new(Options.Create(new JwtSettings
    {
        SecretKey = TestSecretKey,
        Issuer = "adsus-test-issuer",
        Audience = "adsus-test-audience",
        ExpiryMinutes = 60,
    }));

    [Fact]
    public void GenerateAccessToken_ValidUser_IncludesCorrectClaims()
    {
        var user = BuildUser(UserRole.Doctor);

        var token = _sut.GenerateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.UserId.ToString(), jwt.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal(user.Phone, jwt.Claims.Single(c => c.Type == ClaimTypes.MobilePhone).Value);
        Assert.Equal(user.FullName, jwt.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("DOCTOR", jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal(user.UserId.ToString(), jwt.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
    }

    [Theory]
    [InlineData(UserRole.Admin, "ADMIN")]
    [InlineData(UserRole.Doctor, "DOCTOR")]
    [InlineData(UserRole.Nurse, "NURSE")]
    [InlineData(UserRole.Patient, "PATIENT")]
    public void GenerateAccessToken_RoleClaim_IsUppercaseApiString(UserRole role, string expected)
    {
        var user = BuildUser(role);

        var token = _sut.GenerateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(expected, jwt.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public void GenerateAccessToken_SetsIssuerAudienceAndExpiryFromSettings()
    {
        var user = BuildUser(UserRole.Patient);
        var before = DateTime.UtcNow;

        var token = _sut.GenerateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("adsus-test-issuer", jwt.Issuer);
        Assert.Contains("adsus-test-audience", jwt.Audiences);
        // So sánh trong khoảng vài giây thay vì bằng tuyệt đối, vì token sinh ra sau "before"
        // vài mili-giây — ExpiryMinutes=60 nên hạn phải rơi đúng khoảng đó.
        Assert.InRange(jwt.ValidTo, before.AddMinutes(60).AddSeconds(-5), before.AddMinutes(60).AddSeconds(5));
    }

    [Fact]
    public void GenerateAccessToken_EachCall_HasUniqueJti()
    {
        var user = BuildUser(UserRole.Patient);
        var handler = new JwtSecurityTokenHandler();

        var jtiMot = handler.ReadJwtToken(_sut.GenerateAccessToken(user)).Id;
        var jtiHai = handler.ReadJwtToken(_sut.GenerateAccessToken(user)).Id;

        Assert.NotEqual(jtiMot, jtiHai);
    }

    [Fact]
    public void Constructor_MissingSecretKey_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new JwtTokenService(Options.Create(new JwtSettings { SecretKey = "" })));
    }

    private static User BuildUser(UserRole role) => new()
    {
        UserId = Guid.NewGuid(),
        Phone = "0900000001",
        FullName = "Nguyễn Văn A",
        Email = "test@adsus.test",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test@123"),
        Status = UserStatus.Active,
        Role = role,
        MustChangePassword = false,
    };
}
