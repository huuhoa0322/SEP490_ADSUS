using ADSUS_BE.BLL.Auth.DTOs;
using ADSUS_BE.BLL.Auth.Validators;
using Xunit;

namespace ADSUS_BE.UnitTests.Auth;

/// <summary>
/// P12, thêm 14/08/2026 — LoginRequestValidator trước đó chỉ được đụng gián tiếp qua
/// LoginValidate_DoesNotEnforcePhoneNumberFormat (UserAccountRequestValidatorTests.cs, Module 2,
/// dùng để đối chiếu với 2 validator kia), chưa có test nào phủ đúng 3 luật của chính nó.
/// Coverage target cho Validator là 100% (unit-test-convention.md §1).
/// </summary>
public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_IsAccepted()
    {
        var result = _validator.Validate(new LoginRequest
        {
            PhoneNumber = "0900000001",
            Password = "Aa123456@",
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PhoneNumberEmpty_IsRejected()
    {
        var result = _validator.Validate(new LoginRequest { PhoneNumber = "", Password = "Aa123456@" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Phone number is required.");
    }

    [Fact]
    public void Validate_PasswordEmpty_IsRejected()
    {
        var result = _validator.Validate(new LoginRequest { PhoneNumber = "0900000001", Password = "" });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password is required.");
    }

    [Fact]
    public void Validate_PhoneNumberExactlyFifteenCharacters_IsAccepted()
    {
        var result = _validator.Validate(new LoginRequest
        {
            PhoneNumber = new string('0', 15),
            Password = "Aa123456@",
        });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_PhoneNumberExceedsFifteenCharacters_IsRejected()
    {
        var result = _validator.Validate(new LoginRequest
        {
            PhoneNumber = new string('0', 16),
            Password = "Aa123456@",
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Phone number must not exceed 15 characters.");
    }
}
