using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Validators;
using Xunit;

namespace ADSUS_BE.UnitTests.Auth;

/// <summary>
/// Password policy from TDS §4.3: 8–72 characters, at least one uppercase letter and one
/// digit, and the confirmation must match.
///
/// The 72-character ceiling is BCrypt's limit — anything longer is silently truncated, so
/// accepting it would let a user set a password that is not actually what they typed.
///
/// The UCS explicitly states that "the new password must differ from the old one" is NOT a
/// rule, so the last test here pins that absence deliberately.
/// </summary>
public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _sut = new();

    [Fact]
    public void Valid_PasswordMeetingEveryRule_Passes()
    {
        var result = _sut.Validate(Request("Valid123"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("Ab1")]        // too short
    [InlineData("Abc123")]     // 6 characters, still short
    public void Invalid_PasswordShorterThanEightCharacters_Fails(string password)
    {
        var result = _sut.Validate(Request(password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_PasswordLongerThanSeventyTwoCharacters_Fails()
    {
        // 73 characters — one past BCrypt's limit.
        var password = new string('A', 72) + "1";

        var result = _sut.Validate(Request(password));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_PasswordWithoutUppercase_Fails()
    {
        var result = _sut.Validate(Request("lowercase123"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_PasswordWithoutDigit_Fails()
    {
        var result = _sut.Validate(Request("NoDigitsHere"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Invalid_ConfirmationDoesNotMatch_Fails()
    {
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "Test@123",
            NewPassword = "Valid123",
            ConfirmNewPassword = "Different1",
        };

        var result = _sut.Validate(request);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Valid_CurrentPasswordMissing_PassesFormatValidation()
    {
        // Sửa 06/08/2026 — CurrentPassword không còn NotEmpty ở đây: có bắt buộc hay không phụ
        // thuộc User.MustChangePassword, dữ liệu DB mà validator không truy cập được. Luật
        // thật nằm ở AuthService.ChangePasswordAsync — xem ChangePasswordServiceTests.
        var request = new ChangePasswordRequest
        {
            CurrentPassword = null,
            NewPassword = "Valid123",
            ConfirmNewPassword = "Valid123",
        };

        var result = _sut.Validate(request);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Valid_NewPasswordSameAsCurrent_Passes()
    {
        // Deliberate: the UCS says re-using the old password is NOT forbidden. Adding that
        // rule would require a TDS change first, so this test guards against someone
        // introducing it silently.
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "Same1234",
            NewPassword = "Same1234",
            ConfirmNewPassword = "Same1234",
        };

        var result = _sut.Validate(request);

        Assert.True(result.IsValid);
    }

    private static ChangePasswordRequest Request(string newPassword) => new()
    {
        CurrentPassword = "Test@123",
        NewPassword = newPassword,
        ConfirmNewPassword = newPassword,
    };
}
